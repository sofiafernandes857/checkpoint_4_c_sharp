using System;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;

namespace ExpenseHub.Api.Services;

/// <summary>Expense transitions independent of persistence and HTTP.</summary>
public static class ExpenseWorkflowRules
{
    /// <summary>Approves a submitted expense belonging to another user.</summary>
    /// <param name="expense">Target expense.</param>
    /// <param name="actor">Authenticated approver.</param>
    /// <param name="now">Server instant.</param>
    /// <returns>Audit entry to persist atomically.</returns>
    public static ExpenseHistory Approve(Expense expense, ClaimsPrincipal actor, DateTimeOffset now)
    {
        string actorId = ExpenseAccessRules.RequireDecision(expense, actor);
        return Transition(expense, actorId, now, ExpenseStatus.Approved);
    }

    /// <summary>Rejects a submitted expense with a validated reason.</summary>
    /// <param name="expense">Target expense.</param>
    /// <param name="actor">Authenticated approver.</param>
    /// <param name="justification">Reason containing 10 to 500 trimmed characters.</param>
    /// <param name="now">Server instant.</param>
    /// <returns>Audit entry to persist atomically.</returns>
    public static ExpenseHistory Reject(Expense expense, ClaimsPrincipal actor, string? justification, DateTimeOffset now)
    {
        string actorId = ExpenseAccessRules.RequireDecision(expense, actor);
        string reason = justification?.Trim() ?? string.Empty;
        if (reason.Length < 10 || reason.Length > 500)
        {
            throw new ExpenseOperationException(400, "Justification must contain 10 to 500 characters after trimming.");
        }

        ExpenseHistory history = Transition(expense, actorId, now, ExpenseStatus.Rejected);
        history.Justification = reason;
        return history;
    }

    private static ExpenseHistory Transition(Expense expense, string actorId, DateTimeOffset now, ExpenseStatus next)
    {
        ExpenseStatus previous = expense.Status;
        expense.Status = next;
        expense.UpdatedAtUtc = now.ToUniversalTime();
        expense.Version = Guid.NewGuid();
        return new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = next.ToString(),
            ActorId = actorId,
            OccurredAtUtc = now.ToUniversalTime(),
            PreviousStatus = previous,
            NewStatus = next,
        };
    }

    /// <summary>Submits an owned draft and prepares its audit entry.</summary>
    /// <param name="expense">Persisted expense.</param>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="now">Server instant.</param>
    /// <returns>The history entry to persist with the transition.</returns>
    public static ExpenseHistory Submit(Expense expense, ClaimsPrincipal actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(expense);
        string actorId = ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Employee);

        ExpenseAccessRules.RequireOwnedDraft(expense, actorId);
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
