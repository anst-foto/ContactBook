using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContactBook.WebApi.DataBase;

/// <summary>
/// Хелпер для применения миграций EF Core при старте приложения.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Применяет только те миграции, которых ещё нет в таблице истории миграций.
    /// Если применять нечего — метод завершается без обращения к БД на запись.
    /// </summary>
    /// <param name="services">
    /// Провайдер сервисов приложения (обычно <c>app.Services</c>).
    /// Внутри создаётся собственный scope, передавать scoped-сервисы не нужно.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после применения всех ожидающих миграций.</returns>
    /// <exception cref="InvalidOperationException">
    /// Бросается, если <see cref="DataBaseContext"/> не зарегистрирован в DI.
    /// </exception>
    /// <exception cref="Npgsql.PostgresException">
    /// Пробрасывается как есть при ошибках на стороне PostgreSQL
    /// (например, отсутствует тип <c>contact_type</c>).
    /// </exception>
    /// <remarks>
    /// При запуске нескольких реплик сервиса одновременно метод не защищён
    /// от гонки. Для таких сценариев миграции следует выносить в отдельный
    /// шаг деплоя или оборачивать в advisory-lock.
    /// </remarks>
    public static async Task MigrateIfNeededAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        DataBaseContext db = scope.ServiceProvider.GetRequiredService<DataBaseContext>();
        ILogger logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigrator");

        List<string> pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken))
            .ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Миграции не требуются: база в актуальном состоянии.");
            return;
        }

        logger.LogInformation(
            "Будут применены миграции ({Count}): {Migrations}",
            pending.Count,
            string.Join(", ", pending));

        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Миграции успешно применены.");
    }
}