using System;
using System.Collections.Generic;
using System.Text.Json;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;

namespace ExpenseHub.Api.Services;

/// <summary>Draft validation and mutation independent of persistence.</summary>
public static class ExpenseDraftRules
{
    /// <summary>Validates editable values against a supplied server date.</summary>
    /// <param name="request">Editable values.</param>
    /// <param name="today">Server date in UTC.</param>
    public static void Validate(ExpenseDraftRequest request, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(request);
        string description = request.Description?.Trim() ?? string.Empty;

        if (description.Length < 10 || description.Length > 500)
        {
            throw new ExpenseOperationException(400, "Description must contain 10 to 500 characters after trimming.");
        }

        if (request.Amount < 0.01m || request.Amount > int.MaxValue)
        {
            throw new ExpenseOperationException(400, "Amount must be between 0.01 and 2147483647 reais.");
        }

        if (request.ExpenseDate is null || request.ExpenseDate == DateOnly.MinValue || request.ExpenseDate > today)
        {
            throw new ExpenseOperationException(400, "Expense date must be valid and cannot be in the future.");
        }

        if (request.CategoryId is null || request.CategoryId == Guid.Empty)
        {
            throw new ExpenseOperationException(400, "A category identifier is required.");
        }
    }

    /// <summary>Creates a draft and its initial audit entry using server-owned values.</summary>
    /// <param name="request">Validated editable fields.</param>
    /// <param name="actorId">Authenticated employee.</param>
    /// <param name="now">Server instant.</param>
    /// <returns>The draft and its history entry.</returns>
    public static (Expense Expense, ExpenseHistory History) Create(
        ExpenseDraftRequest request, string actorId, DateTimeOffset now)
    {
        Validate(request, DateOnly.FromDateTime(now.UtcDateTime));
        Expense expense = new()
        {
            OwnerId = actorId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate!.Value,
            CategoryId = request.CategoryId!.Value,
            CreatedAtUtc = now.ToUniversalTime(),
            UpdatedAtUtc = now.ToUniversalTime(),
        };
        return (expense, History(expense, actorId, now, "Created", null, null));
    }

    /// <summary>Updates an owned draft and records the old and new values of changed fields.</summary>
    /// <param name="expense">Persisted draft.</param>
    /// <param name="request">Editable values.</param>
    /// <param name="actorId">Authenticated employee.</param>
    /// <param name="now">Server instant.</param>
    /// <returns>The audit entry associated with this mutation.</returns>
    public static ExpenseHistory Edit(Expense expense, ExpenseDraftRequest request, string actorId, DateTimeOffset now)
    {
        ExpenseAccessRules.RequireOwnedDraft(expense, actorId);
        Validate(request, DateOnly.FromDateTime(now.UtcDateTime));
        Dictionary<string, object> changes = new();
        AddChange(changes, nameof(Expense.Description), expense.Description, request.Description.Trim());
        AddChange(changes, nameof(Expense.Amount), expense.Amount, request.Amount);
        AddChange(changes, nameof(Expense.ExpenseDate), expense.ExpenseDate, request.ExpenseDate!.Value);
        AddChange(changes, nameof(Expense.CategoryId), expense.CategoryId, request.CategoryId!.Value);
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate.Value;
        expense.CategoryId = request.CategoryId.Value;
        expense.UpdatedAtUtc = now.ToUniversalTime();
        expense.Version = Guid.NewGuid();
        return History(expense, actorId, now, "Edited", ExpenseStatus.Draft, JsonSerializer.Serialize(changes));
    }

    private static void AddChange<T>(Dictionary<string, object> changes, string field, T previous, T current)
    {
        if (!EqualityComparer<T>.Default.Equals(previous, current))
        {
            changes.Add(field, new { previous, current });
        }
    }

    private static ExpenseHistory History(Expense expense, string actorId, DateTimeOffset now,
        string action, ExpenseStatus? previousStatus, string? changes) => new()
        {
            ExpenseId = expense.Id,
            ActorId = actorId,
            OccurredAtUtc = now.ToUniversalTime(),
            Action = action,
            PreviousStatus = previousStatus,
            NewStatus = expense.Status,
            Changes = changes,
        };
}
