using System;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;

namespace ExpenseHub.Api.Services;

/// <summary>Expense transitions independent of persistence and HTTP.</summary>
public static class ExpenseWorkflowRules
{
    /// <summary>Submits an owned draft and prepares its audit entry.</summary>
    /// <param name="expense">Persisted expense.</param>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="now">Server instant.</param>
    /// <returns>The history entry to persist with the transition.</returns>
    public static ExpenseHistory Submit(Expense expense, ClaimsPrincipal actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(expense);
        string actorId = ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Employee);

        if (!string.Equals(expense.OwnerId, actorId, StringComparison.Ordinal))
        {
            throw new ExpenseOperationException(404, "Expense not found.");
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            throw new ExpenseOperationException(409, "Only drafts can be submitted.");
        }

        expense.Status = ExpenseStatus.Submitted;
        expense.UpdatedAtUtc = now.ToUniversalTime();
        expense.Version = Guid.NewGuid();
        return new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = "Submitted",
            ActorId = actorId,
            OccurredAtUtc = now.ToUniversalTime(),
            PreviousStatus = ExpenseStatus.Draft,
            NewStatus = ExpenseStatus.Submitted,
        };
    }
}
