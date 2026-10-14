using System;
using System.Linq.Expressions;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;

namespace ExpenseHub.Api.Security;

/// <summary>Reusable role and ownership rules, including a database-translatable visibility filter.</summary>
public static class ExpenseAccessRules
{
    /// <summary>Roles granting functional read access, without implicit Admin access.</summary>
    public const string ReaderRoles = ApplicationRoles.Employee + "," + ApplicationRoles.Approver + "," +
        ApplicationRoles.Finance + "," + ApplicationRoles.Auditor;

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

    /// <summary>Requires draft ownership; invisible ownership failures do not disclose existence.</summary>
    /// <param name="expense">Target expense.</param>
    /// <param name="actorId">Authenticated employee identifier.</param>
    public static void RequireOwnedDraft(Expense expense, string actorId)
    {
        ArgumentNullException.ThrowIfNull(expense);

        if (!string.Equals(expense.OwnerId, actorId, StringComparison.Ordinal))
        {
            throw new ExpenseOperationException(404, "Expense not found.");
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            throw new ExpenseOperationException(409, "Only owned drafts can be edited or submitted.");
        }
    }

    /// <summary>Requires Approver, a non-owned expense and Submitted before a future decision.</summary>
    /// <param name="expense">Target expense.</param>
    /// <param name="actor">Authenticated principal.</param>
    /// <returns>The authenticated decision actor.</returns>
    public static string RequireDecision(Expense expense, ClaimsPrincipal actor) =>
        RequireNonOwnedState(expense, actor, ApplicationRoles.Approver, ExpenseStatus.Submitted);

    /// <summary>Requires Finance, a non-owned expense and Approved before a future payment.</summary>
    /// <param name="expense">Target expense.</param>
    /// <param name="actor">Authenticated principal.</param>
    /// <returns>The authenticated payment actor.</returns>
    public static string RequirePayment(Expense expense, ClaimsPrincipal actor) =>
        RequireNonOwnedState(expense, actor, ApplicationRoles.Finance, ExpenseStatus.Approved);

    private static string RequireNonOwnedState(Expense expense, ClaimsPrincipal actor,
        string requiredRole, ExpenseStatus requiredState)
    {
        ArgumentNullException.ThrowIfNull(expense);
        string actorId = RequireRole(actor, requiredRole);

        if (string.Equals(expense.OwnerId, actorId, StringComparison.Ordinal))
        {
            throw new ExpenseOperationException(403, "You cannot approve, reject or pay your own expense.");
        }

        if (expense.Status != requiredState)
        {
            throw new ExpenseOperationException(409, $"The expense must be {requiredState} for this operation.");
        }

        return actorId;
    }
}
