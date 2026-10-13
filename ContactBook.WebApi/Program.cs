using System;

using ContactBook.WebApi.DataBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddNLog(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultDocker")
                       ?? builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException("Не задана строка подключения (ConnectionStrings:DefaultDocker).");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<ContactType>(ContactModelConfiguration.EnumTypeName);
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<DataBaseContext>(options =>
    options.UseNpgsql(dataSource, npgsql =>
    {
        npgsql.MigrationsAssembly(typeof(DataBaseContext).Assembly.FullName);
        npgsql.MigrationsHistoryTable("__ef_migrations_history");
    }));

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DataBaseContext>("postgres");

var app = builder.Build();

var runMigrations = builder.Configuration.GetValue("RunMigrations", defaultValue: true);
if (runMigrations)
{
    await app.Services.MigrateIfNeededAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.Run();