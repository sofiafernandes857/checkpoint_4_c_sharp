using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ExpenseHub.Api.Contracts;

/// <summary>Replaces the roles assigned to an existing user.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateRolesRequest
{
    /// <summary>Gets or sets the complete desired role set; empty removes all roles.</summary>
    [Required]
    [MaxLength(5)]
    public string[] Roles { get; set; } = null!;
}
