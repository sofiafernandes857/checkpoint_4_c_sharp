using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Services;

/// <summary>Coordinates expense rules and atomic persistence.</summary>
public sealed class ExpenseService
{
    private readonly ExpenseHubDbContext _database;
    private readonly TimeProvider _clock;

    /// <summary>Initializes the service with scoped persistence and an injectable clock.</summary>
    /// <param name="database">Scoped relational context.</param>
    /// <param name="clock">Server clock.</param>
    public ExpenseService(ExpenseHubDbContext database, TimeProvider clock)
    {
        _database = database;
        _clock = clock;
    }

    /// <summary>Creates a draft and its history atomically.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="request">Editable fields.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The persisted draft.</returns>
    public async Task<Expense> CreateAsync(ClaimsPrincipal actor, ExpenseDraftRequest request,
        CancellationToken cancellationToken)
    {
        string actorId = EmployeeId(actor);
        DateTimeOffset now = _clock.GetUtcNow();
        ExpenseDraftRules.Validate(request, DateOnly.FromDateTime(now.UtcDateTime));
        await ValidateCategoryAsync(request.CategoryId!.Value, cancellationToken);
        (Expense expense, ExpenseHistory history) = ExpenseDraftRules.Create(request, actorId, now);
        _database.Expenses.Add(expense);
        _database.ExpenseHistories.Add(history);
        await SaveAsync(cancellationToken);
        return expense;
    }

    /// <summary>Edits only a draft owned by the authenticated employee.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="id">Expense identifier.</param>
    /// <param name="request">Editable fields.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The updated draft.</returns>
    public async Task<Expense> EditAsync(ClaimsPrincipal actor, Guid id, ExpenseDraftRequest request,
        CancellationToken cancellationToken)
    {
        string actorId = EmployeeId(actor);
        Expense expense = await _database.Expenses.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerId == actorId, cancellationToken)
            ?? throw new ExpenseOperationException(404, "Expense not found.");

        if (expense.Status != ExpenseStatus.Draft)
        {
            throw new ExpenseOperationException(409, "Only drafts can be edited.");
        }

        DateTimeOffset now = _clock.GetUtcNow();
        ExpenseDraftRules.Validate(request, DateOnly.FromDateTime(now.UtcDateTime));
        await ValidateCategoryAsync(request.CategoryId!.Value, cancellationToken);
        _database.ExpenseHistories.Add(ExpenseDraftRules.Edit(expense, request, actorId, now));
        await SaveAsync(cancellationToken);
        return expense;
    }

    /// <summary>Lists available categories for an authenticated employee.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Categories ordered by name.</returns>
    public async Task<IReadOnlyList<ExpenseCategory>> CategoriesAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        EmployeeId(actor);
        return await _database.ExpenseCategories.AsNoTracking().OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    private static string EmployeeId(ClaimsPrincipal actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string? id = actor.FindFirstValue(ClaimTypes.NameIdentifier);

        if (actor.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id))
        {
            throw new ExpenseOperationException(401, "Authentication is required.");
        }

        if (!actor.IsInRole(ApplicationRoles.Employee))
        {
            throw new ExpenseOperationException(403, "The Employee role is required.");
        }

        return id;
    }

    private async Task ValidateCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _database.ExpenseCategories.AnyAsync(category => category.Id == id, cancellationToken))
        {
            throw new ExpenseOperationException(400, "Category does not exist.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _database.ChangeTracker.Clear();
            throw new ExpenseOperationException(409, "The expense changed concurrently. Reload and retry.");
        }
    }
}
