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

    /// <summary>Registers an account without assigning any roles.</summary>
    /// <param name="request">Validated registration fields.</param>
    /// <returns>The new identifier or validation errors.</returns>
    [AllowAnonymous]
    [HttpPost("/register")]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request)
    {
        IdentityUser user = new() { UserName = request.Email, Email = request.Email };
        IdentityResult result = await _users.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return StatusCode(201, new { user.Id, user.Email });
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
