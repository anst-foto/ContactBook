using System;
using System.IO;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ContactBook.WebApi;

/// <summary>
/// Методы расширения для регистрации и настройки Swagger/OpenAPI.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Регистрирует генератор OpenAPI-документа и UI Swagger.
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            // --- Метаданные документа ---
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ContactBook API",
                Version = "v1",
                Description =
                    "API контактной книги: хранение и управление контактами (email/телефон). " +
                    "Все эндпоинты работают поверх PostgreSQL через EF Core.",
                Contact = new OpenApiContact { Name = "ContactBook team", Email = "team@example.com" },
                License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
            });

            // --- XML-комментарии из сборки ---
            string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, true);
            }

            // --- Enum как строка (Email, Phone вместо 0, 1) ---
            options.UseInlineDefinitionsForEnums();

            // --- Заготовка под Bearer/JWT (включаем, когда появится авторизация) ---
            // options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
            // options.AddSecurityRequirement(...);

            // --- Группировка эндпоинтов по тегам из [EndpointSummary]/[Tags] ---
            options.OrderActionsBy(api =>
                $"{api.ActionDescriptor.RouteValues["controller"]}_{api.HttpMethod}");
        });

        return services;
    }

    /// <summary>
    /// Подключает middleware Swagger UI.
    /// </summary>
    /// <param name="app">Экземпляр приложения.</param>
    /// <returns>Тот же экземпляр для цепочки вызовов.</returns>
    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ContactBook API v1");
            options.DocumentTitle = "ContactBook API";
            options.DisplayRequestDuration(); // показывает время ответа
            options.EnableTryItOutByDefault(); // кнопка Try it out активна сразу
            options.EnableDeepLinking(); // прямые ссылки на эндпоинты
            options.EnableFilter(); // поиск по эндпоинтам
            options.RoutePrefix = "swagger"; // UI по /swagger, JSON по /swagger/v1/swagger.json
        });

        return app;
    }
}