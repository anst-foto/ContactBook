using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading;

using ContactBook.WebApi.DataBase;
using ContactBook.WebApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using Npgsql;

#region Builder

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddNLog(builder.Configuration);

string connectionString = builder.Configuration.GetConnectionString("DefaultDocker")
                          ?? builder.Configuration.GetConnectionString("Default")
                          ?? throw new InvalidOperationException(
                              "Не задана строка подключения (ConnectionStrings:DefaultDocker).");

NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
dataSourceBuilder.MapEnum<ContactType>(ContactModelConfiguration.EnumTypeName);
NpgsqlDataSource dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<DataBaseContext>(options =>
    options.UseNpgsql(dataSource, npgsql =>
    {
        npgsql.MigrationsAssembly(typeof(DataBaseContext).Assembly.FullName);
        npgsql.MigrationsHistoryTable("__ef_migrations_history");
    }));

builder.Services.AddSwaggerDocumentation();
builder.Services.AddHealthChecks().AddDbContextCheck<DataBaseContext>("postgres");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

#endregion

#region Application

WebApplication app = builder.Build();

bool runMigrations = builder.Configuration.GetValue("RunMigrations", true);
if (runMigrations)
{
    await app.Services.MigrateIfNeededAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");

#endregion

#region Endpoints

const int MinShortIdLength = 4;
const int ShortIdLength = 8;
Regex hexRegex = MyRegex();

RouteGroupBuilder contacts = app
    .MapGroup("/api/v1/contacts")
    .WithTags("Contacts");

contacts.MapGet("/", async (DataBaseContext db, string? search, CancellationToken ct) =>
    {
        IQueryable<ContactModel> query = db.Contacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern) ||
                EF.Functions.ILike(c.Contact, pattern));
        }

        List<ContactResponse> items = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new ContactResponse(
                c.Id,
                c.Id.ToString("N").Substring(0, ShortIdLength),
                c.Name,
                c.Contact,
                c.Type))
            .ToListAsync(ct);

        return Results.Ok(items);
    })
    .WithName("GetContacts")
    .WithSummary("Список контактов с поиском по имени или контакту")
    .Produces<IReadOnlyList<ContactResponse>>(StatusCodes.Status200OK);

contacts.MapGet("/{id:guid}", async (Guid id, DataBaseContext db, CancellationToken ct) =>
    {
        ContactModel? contact = await db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return contact is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(contact, ShortIdLength));
    })
    .WithName("GetContactById")
    .WithSummary("Получить контакт по полному GUID")
    .Produces<ContactResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

contacts.MapGet("/by-short-id/{shortId}", async (string shortId, DataBaseContext db, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shortId"] = ["Обязательное поле."]
            });
        }

        string normalized = shortId.Replace("-", string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length < MinShortIdLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shortId"] = [$"Минимальная длина — {MinShortIdLength} hex-символа."]
            });
        }

        if (!hexRegex.IsMatch(normalized))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shortId"] = ["Допустимы только hex-символы 0-9, a-f (дефисы игнорируются)."]
            });
        }

        List<ContactModel> matches = await db.Contacts
            .AsNoTracking()
            .Where(c => c.Id.ToString().Replace("-", "").StartsWith(normalized))
            .Take(2)
            .ToListAsync(ct);

        return matches.Count switch
        {
            0 => Results.NotFound(),
            1 => Results.Ok(ToResponse(matches[0], ShortIdLength)),
            _ => Results.Conflict(new
            {
                message = "Короткий ID неоднозначен, найдено несколько контактов.",
                hint = "Уточните префикс или используйте полный GUID."
            })
        };
    })
    .WithName("GetContactByShortId")
    .WithSummary("Получить контакт по короткому ID (hex-префикс GUID, минимум 4 символа)")
    .Produces<ContactResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict);

contacts.MapPost("", async (ContactCreateRequest request, DataBaseContext db, CancellationToken ct) =>
    {
        Dictionary<string, string[]> errors = Validate(request.Name, request.Contact);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        ContactModel entity = new()
        {
            Name = request.Name.Trim(),
            Contact = request.Contact.Trim(),
            Type = request.Type
        };

        db.Contacts.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(new { message = "Контакт с таким значением и типом уже существует." });
        }

        return Results.CreatedAtRoute(
            "GetContactById",
            new { id = entity.Id },
            ToResponse(entity, ShortIdLength));
    })
    .WithName("CreateContact")
    .WithSummary("Создать контакт")
    .Produces<ContactResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status409Conflict);

contacts.MapPut("", async (ContactUpdateRequest request, DataBaseContext db, CancellationToken ct) =>
    {
        if (request.Id == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["Идентификатор обязателен и не может быть пустым (Guid.Empty)."]
            });
        }

        Dictionary<string, string[]> errors = Validate(request.Name, request.Contact);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        ContactModel? entity = await db.Contacts
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (entity is null)
        {
            return Results.NotFound();
        }

        entity.Name = request.Name.Trim();
        entity.Contact = request.Contact.Trim();
        entity.Type = request.Type;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(new { message = "Контакт с таким значением и типом уже существует." });
        }

        return Results.Ok(ToResponse(entity, ShortIdLength));
    })
    .WithName("UpdateContact")
    .WithSummary("Обновить контакт (ID передаётся в теле запроса)")
    .Produces<ContactResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict);

contacts.MapDelete("/{id:guid}", async (Guid id, DataBaseContext db, CancellationToken ct) =>
    {
        if (id == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["Идентификатор не может быть пустым (Guid.Empty)."]
            });
        }

        ContactModel? entity = await db.Contacts
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null)
        {
            return Results.NotFound();
        }

        db.Contacts.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    })
    .WithName("DeleteContact")
    .WithSummary("Удалить контакт")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);

#endregion

#region Run

app.MapFallbackToFile("index.html");

await app.RunAsync();

#endregion

#region Helpers

static Dictionary<string, string[]> Validate(string? name, string? contact)
{
    Dictionary<string, string[]> errors = new();

    if (string.IsNullOrWhiteSpace(name))
    {
        errors["name"] = ["Имя обязательно."];
    }
    else if (name.Length > 200)
    {
        errors["name"] = ["Имя не должно превышать 200 символов."];
    }

    if (string.IsNullOrWhiteSpace(contact))
    {
        errors["contact"] = ["Значение контакта обязательно."];
    }
    else if (contact.Length > 320)
    {
        errors["contact"] = ["Значение контакта не должно превышать 320 символов."];
    }

    return errors;
}

static ContactResponse ToResponse(ContactModel c, int shortIdLength)
{
    return new ContactResponse(
        c.Id,
        c.Id.ToString("N")[..shortIdLength],
        c.Name,
        c.Contact,
        c.Type);
}

static bool IsUniqueViolation(DbUpdateException ex)
{
    return ex.InnerException is PostgresException { SqlState: "23505" };
}

#endregion

#region DTOs

public sealed record ContactCreateRequest(string Name, string Contact, ContactType Type);

public sealed record ContactUpdateRequest(Guid Id, string Name, string Contact, ContactType Type);

public sealed record ContactResponse(Guid Id, string ShortId, string Name, string Contact, ContactType Type);

#endregion

internal partial class Program
{
    [GeneratedRegex("^[0-9a-f]+$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}