using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Contracts;

/// <summary>Credentials accepted by the public login endpoint.</summary>
public sealed class LoginRequest
{
    /// <summary>Gets or sets the registered email.</summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the password used only for credential verification.</summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
