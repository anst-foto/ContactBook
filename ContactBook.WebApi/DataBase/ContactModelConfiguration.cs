using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactBook.WebApi.DataBase;

/// <summary>
/// Конфигурация EF Core для <see cref="ContactModel"/>.
/// </summary>
/// <remarks>
/// Применяется автоматически через <c>ModelBuilder.ApplyConfiguration</c>
/// в <see cref="DataBaseContext.OnModelCreating"/>.
/// </remarks>
public sealed class ContactModelConfiguration : IEntityTypeConfiguration<ContactModel>
{
    /// <summary>Имя таблицы в PostgreSQL.</summary>
    public const string TableName = "contacts";

    /// <summary>Имя нативного PostgreSQL-enum-типа для <see cref="ContactType"/>.</summary>
    public const string EnumTypeName = "contact_type";

    /// <summary>Имя уникального индекса по паре <c>(type, contact)</c>.</summary>
    public const string UniqueIndexName = "ix_contacts_type_contact";

    /// <summary>
    /// Настраивает отображение <see cref="ContactModel"/> на таблицу <c>contacts</c>.
    /// </summary>
    /// <param name="builder">Построитель конфигурации сущности.</param>
    public void Configure(EntityTypeBuilder<ContactModel> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Contact)
            .HasColumnName("contact")
            .HasColumnType("character varying(320)")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(c => c.Type)
            .HasColumnName("type")
            .HasColumnType(EnumTypeName)
            .IsRequired();

        builder.HasIndex(c => new { c.Type, c.Contact })
            .HasDatabaseName(UniqueIndexName)
            .IsUnique();
    }
}