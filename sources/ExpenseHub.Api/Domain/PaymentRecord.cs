using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Persisted PaymentRecord data.</summary>
public sealed class PaymentRecord
{
    /// <summary>Payment identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Paid expense identifier.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>Authenticated finance actor identifier.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Payment instant in UTC.</summary>
    public DateTimeOffset PaidAtUtc { get; set; }
}
