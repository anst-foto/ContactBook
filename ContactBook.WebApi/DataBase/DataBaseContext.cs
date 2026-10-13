using Microsoft.EntityFrameworkCore;

namespace ContactBook.WebApi.DataBase;

/// <summary>
/// Контекст EF Core для базы контактной книги.
/// </summary>
/// <remarks>
/// Регистрируется в DI через <c>AddDbContext</c> с <c>NpgsqlDataSource</c>,
/// в котором заранее замаплен enum <see cref="ContactType"/> на
/// PostgreSQL-тип <c>contact_type</c>.
/// </remarks>
public class DataBaseContext : DbContext
{
    /// <summary>
    /// Создаёт контекст с указанными опциями.
    /// </summary>
    /// <param name="options">Опции, предоставленные DI-контейнером.</param>
    public DataBaseContext(DbContextOptions<DataBaseContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Набор записей контактной книги.
    /// </summary>
    public DbSet<ContactModel> Contacts => Set<ContactModel>();

    /// <summary>
    /// Настраивает модель данных: регистрирует нативный enum-тип и применяет
    /// конфигурации сущностей.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Регистрируем нативный enum-тип для Npgsql (используется миграциями/снапшотом).
        // Сам тип в БД создаётся скриптом initdb/01-init.sql — EF не эмитит CREATE TYPE.
        modelBuilder.HasPostgresEnum<ContactType>(
            name: ContactModelConfiguration.EnumTypeName,
            schema: null);

        modelBuilder.ApplyConfiguration(new ContactModelConfiguration());
    }
}