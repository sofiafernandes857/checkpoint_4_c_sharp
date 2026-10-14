using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ExpenseHub.Api.Contracts;

/// <summary>Public registration input; role fields are not accepted.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegisterRequest
{
    /// <summary>Gets or sets a unique email address.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the password passed to Identity hashing.</summary>
    [Required]
    [StringLength(128, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;
}
