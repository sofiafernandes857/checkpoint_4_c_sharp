using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ExpenseHub.Api.Contracts;

/// <summary>Client-supplied reason for rejecting a submitted expense.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RejectExpenseRequest
{
    /// <summary>Gets or sets the reason, trimmed before validation.</summary>
    [Required]
    public string Justification { get; set; } = string.Empty;
}
