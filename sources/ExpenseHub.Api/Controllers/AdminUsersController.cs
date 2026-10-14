using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpenseHub.Api.Controllers;

/// <summary>Administrative user queries and atomic role replacement.</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = ApplicationRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserManager<IdentityUser> _users;
    private readonly ExpenseHubDbContext _database;

    /// <summary>Initializes administration with relational Identity persistence.</summary>
    /// <param name="users">Identity user manager.</param>
    /// <param name="database">The same scoped context used by Identity.</param>
    public AdminUsersController(UserManager<IdentityUser> users, ExpenseHubDbContext database)
    {
        _users = users;
        _database = database;
    }

    /// <summary>Lists account identifiers, emails and assigned roles without credential data.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The users available to the administrator.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        List<IdentityUser> users = await _users.Users.AsNoTracking()
            .OrderBy(user => user.Email).ToListAsync(cancellationToken);
        List<object> response = new();

        foreach (IdentityUser user in users)
        {
            IList<string> roles = await _users.GetRolesAsync(user);
            response.Add(new { user.Id, user.Email, roles });
        }

        return Ok(response);
    }

    /// <summary>Replaces a user's complete role set without implicit role creation.</summary>
    /// <param name="id">Target account identifier.</param>
    /// <param name="request">Desired role set.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The updated role set or a consistent problem response.</returns>
    [HttpPut("{id}/roles")]
    public async Task<IActionResult> UpdateRolesAsync(
        string id, UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        string? actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Problem(statusCode: 401, title: "Authenticated user identifier is missing.");
        }

        string? error = RoleChangeRules.GetError(actorId, id, request.Roles);

        if (error is not null)
        {
            return Problem(statusCode: 400, title: "Invalid role assignment.", detail: error);
        }

        await using IDbContextTransaction transaction = await _database.Database.BeginTransactionAsync(cancellationToken);
        IdentityUser? user = await _users.FindByIdAsync(id);

        if (user is null)
        {
            return Problem(statusCode: 404, title: "User not found.");
        }

        IList<string> currentRoles = await _users.GetRolesAsync(user);
        string[] toRemove = currentRoles.Except(request.Roles, StringComparer.Ordinal).ToArray();
        string[] toAdd = request.Roles.Except(currentRoles, StringComparer.Ordinal).ToArray();

        if (toRemove.Length > 0)
        {
            IdentityResult removed = await _users.RemoveFromRolesAsync(user, toRemove);

            if (!removed.Succeeded)
            {
                return RoleFailure(removed);
            }
        }

        if (toAdd.Length > 0)
        {
            IdentityResult added = await _users.AddToRolesAsync(user, toAdd);

            if (!added.Succeeded)
            {
                return RoleFailure(added);
            }
        }

        // Account changes must be incorporated into a newly issued token.
        IdentityResult updated = await _users.UpdateSecurityStampAsync(user);

        if (!updated.Succeeded)
        {
            return RoleFailure(updated);
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(new { user.Id, roles = request.Roles, message = "Log in again to receive updated roles." });
    }

    private ObjectResult RoleFailure(IdentityResult result)
    {
        if (result.Errors.Any(error => error.Code == "ConcurrencyFailure"))
        {
            return Problem(statusCode: 409, title: "The account changed concurrently. Retry with current data.");
        }

        return Problem(statusCode: 400, title: "Role assignment failed.",
            detail: string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
