using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>Authenticates users with the Identity bearer scheme.</summary>
[ApiController]
public sealed class AuthenticationController : ControllerBase
{
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;

    /// <summary>Initializes the authentication controller.</summary>
    /// <param name="users">Identity user manager.</param>
    /// <param name="signIn">Identity sign-in manager.</param>
    public AuthenticationController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    /// <summary>Issues a bearer credential for a valid, unlocked account.</summary>
    /// <param name="request">Validated credentials.</param>
    /// <returns>The Identity token response or a credential error.</returns>
    [AllowAnonymous]
    [HttpPost("/login")]
    public async Task<IActionResult> LoginAsync(LoginRequest request)
    {
        IdentityUser? user = await _users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return Problem(statusCode: 401, title: "Invalid credentials.");
        }

        _signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
        Microsoft.AspNetCore.Identity.SignInResult result = await _signIn.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Problem(statusCode: 401, title: "Invalid credentials.");
        }

        // The bearer handler has already written accessToken, refreshToken and expiresIn.
        return new EmptyResult();
    }
}
