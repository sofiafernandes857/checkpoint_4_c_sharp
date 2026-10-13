using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Persisted ExpenseCategory data.</summary>
public sealed class ExpenseCategory
{
    /// <summary>Category identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Category name.</summary>
    public string Name { get; set; } = string.Empty;
}
