using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Persisted Expense data.</summary>
public sealed class Expense
{
    /// <summary>Server-generated identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Authenticated owner identifier.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Expense description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Requested amount in Brazilian reais.</summary>
    public decimal Amount { get; set; }

    /// <summary>Date when the expense occurred.</summary>
    public DateOnly ExpenseDate { get; set; }

    /// <summary>Current workflow state.</summary>
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;

    /// <summary>Category identifier.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Creation instant in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last change instant in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Concurrency token renewed on each change.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();
}
