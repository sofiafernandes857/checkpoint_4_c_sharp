using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Persisted ExpenseHistory data.</summary>
public sealed class ExpenseHistory
{
    /// <summary>History entry identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Associated expense identifier.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>Action performed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Authenticated actor identifier.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Event instant in UTC.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>State before the action; null on creation.</summary>
    public ExpenseStatus? PreviousStatus { get; set; }

    /// <summary>State after the action.</summary>
    public ExpenseStatus NewStatus { get; set; }

    /// <summary>Rejection justification when applicable.</summary>
    public string? Justification { get; set; }

    /// <summary>Description of changes to draft fields.</summary>
    public string? Changes { get; set; }
}
