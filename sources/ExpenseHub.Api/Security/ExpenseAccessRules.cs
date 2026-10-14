using System;
using System.Linq.Expressions;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;

namespace ExpenseHub.Api.Security;

/// <summary>Reusable role and ownership rules, including a database-translatable visibility filter.</summary>
public static class ExpenseAccessRules
{
    /// <summary>Requires authentication and a server-issued identifier.</summary>
    /// <param name="actor">Request principal.</param>
    /// <returns>The authenticated identifier.</returns>
    public static string ActorId(ClaimsPrincipal actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string? id = actor.FindFirstValue(ClaimTypes.NameIdentifier);

        if (actor.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id))
        {
            throw new ExpenseOperationException(401, "Authentication is required.");
        }

        return id;
    }

    /// <summary>Requires a functional role independently of controller attributes.</summary>
    /// <param name="actor">Request principal.</param>
    /// <param name="role">Required canonical role.</param>
    /// <returns>The authenticated identifier.</returns>
    public static string RequireRole(ClaimsPrincipal actor, string role)
    {
        string id = ActorId(actor);

        if (!actor.IsInRole(role))
        {
            throw new ExpenseOperationException(403, $"The {role} role is required.");
        }

        return id;
    }

    /// <summary>Builds the union of role visibility before records are materialized.</summary>
    /// <param name="actor">Request principal.</param>
    /// <returns>A predicate shared by list, detail and future history queries.</returns>
    public static Expression<Func<Expense, bool>> Visibility(ClaimsPrincipal actor)
    {
        string id = ActorId(actor);
        bool employee = actor.IsInRole(ApplicationRoles.Employee);
        bool approver = actor.IsInRole(ApplicationRoles.Approver);
        bool finance = actor.IsInRole(ApplicationRoles.Finance);
        bool auditor = actor.IsInRole(ApplicationRoles.Auditor);

        if (!employee && !approver && !finance && !auditor)
        {
            throw new ExpenseOperationException(403, "A functional expense role is required.");
        }

        return expense => auditor ||
            (employee && expense.OwnerId == id) ||
            (approver && expense.Status == ExpenseStatus.Submitted) ||
            (finance && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid));
    }
}
