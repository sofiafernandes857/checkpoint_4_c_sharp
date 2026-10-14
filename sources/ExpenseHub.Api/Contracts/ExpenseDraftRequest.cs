using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ExpenseHub.Api.Contracts;

/// <summary>Only the editable fields of an expense draft.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ExpenseDraftRequest
{
    /// <summary>Gets or sets the description, trimmed before business validation.</summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested amount in reais.</summary>
    [Range(typeof(decimal), "0.01", "2147483647", ParseLimitsInInvariantCulture = true)]
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the expense date, no later than today in UTC.</summary>
    [Required]
    public DateOnly? ExpenseDate { get; set; }

    /// <summary>Gets or sets an existing category identifier.</summary>
    [Required]
    public Guid? CategoryId { get; set; }
}
