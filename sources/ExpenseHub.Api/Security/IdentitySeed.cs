using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ExpenseHub.Api.Security;

/// <summary>Creates roles and only the initial administrator account.</summary>
public static class IdentitySeed
{
    /// <summary>Seeds initial identity data without replacing existing credentials.</summary>
    /// <param name="users">Identity user manager.</param>
    /// <param name="roles">Identity role manager.</param>
    /// <param name="configuration">Secure initial administrator configuration.</param>
    /// <returns>The asynchronous seed operation.</returns>
    public static async Task InitializeAsync(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        IConfiguration configuration)
    {
        foreach (string role in ApplicationRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                EnsureSuccess(await roles.CreateAsync(new IdentityRole(role)));
            }
        }

        // Once an Admin exists, do not create another or restore removed permissions.
        if ((await users.GetUsersInRoleAsync(ApplicationRoles.Admin)).Count > 0)
        {
            return;
        }

        string email = configuration["AdminSeed:Email"] ?? string.Empty;
        string password = configuration["AdminSeed:Password"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Configure AdminSeed:Email and AdminSeed:Password through User Secrets or environment variables before the first run.");
        }

        IdentityUser? existingUser = await users.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("The configured initial administrator email already belongs to a non-Admin account. Refusing automatic promotion.");
        }

        IdentityUser administrator = new() { UserName = email, Email = email };
        EnsureSuccess(await users.CreateAsync(administrator, password));
        EnsureSuccess(await users.AddToRoleAsync(administrator, ApplicationRoles.Admin));
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Identity initialization failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }
}
