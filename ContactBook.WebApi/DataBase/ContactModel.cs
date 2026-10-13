using System;

namespace ContactBook.WebApi.DataBase;

/// <summary>
/// Тип контактных данных.
/// </summary>
/// <remarks>
/// Хранится в PostgreSQL как нативный enum-тип <c>contact_type</c>
/// (создаётся скриптом <c>initdb/01-init.sql</c>, а не миграцией EF).
/// </remarks>
public enum ContactType
{
    /// <summary>Адрес электронной почты.</summary>
    Email = 0,

    /// <summary>Номер телефона.</summary>
    Phone = 1
}

/// <summary>
/// Запись контактной книги: имя и один контакт (email или телефон).
/// </summary>
/// <remarks>
/// Соответствует таблице <c>contacts</c>. Уникальность пары
/// <see cref="Type"/> + <see cref="Contact"/> обеспечивается индексом
/// <c>ix_contacts_type_contact</c>.
/// </remarks>
public class ContactModel
{
    /// <summary>
    /// Уникальный идентификатор записи (UUID v7, монотонный по времени).
    /// </summary>
    /// <remarks>
    /// Значение генерируется на клиенте при создании объекта.
    /// EF сконфигурирован через <c>ValueGeneratedNever</c>, поэтому
    /// при вставке значение берётся как есть, без перегенерации.
    /// </remarks>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Отображаемое имя контакта (например, «Иван Петров» или «Служба поддержки»).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Значение контакта: адрес email или номер телефона,
    /// в зависимости от <see cref="Type"/>.
    /// </summary>
    public required string Contact { get; set; }

    /// <summary>
    /// Тип контакта, определяющий интерпретацию <see cref="Contact"/>.
    /// </summary>
    public ContactType Type { get; set; }
}