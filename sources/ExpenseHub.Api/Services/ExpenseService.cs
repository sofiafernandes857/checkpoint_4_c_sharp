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
        string actorId = ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Employee);
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
        string actorId = ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Employee);
        Expense expense = await _database.Expenses.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerId == actorId, cancellationToken)
            ?? throw new ExpenseOperationException(404, "Expense not found.");

        ExpenseAccessRules.RequireOwnedDraft(expense, actorId);
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
        ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Employee);
        return await _database.ExpenseCategories.AsNoTracking().OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Submits an owned draft with its audit entry in one save.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The submitted expense.</returns>
    public async Task<Expense> SubmitAsync(ClaimsPrincipal actor, Guid id, CancellationToken cancellationToken)
    {
        string actorId = ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Employee);
        Expense expense = await _database.Expenses.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerId == actorId, cancellationToken)
            ?? throw new ExpenseOperationException(404, "Expense not found.");
        _database.ExpenseHistories.Add(ExpenseWorkflowRules.Submit(expense, actor, _clock.GetUtcNow()));
        await SaveAsync(cancellationToken);
        return expense;
    }

    /// <summary>Lists only records visible to the combined roles.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Visible expenses ordered by expense date and identifier.</returns>
    public async Task<IReadOnlyList<Expense>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken) =>
        await _database.Expenses.AsNoTracking().Where(ExpenseAccessRules.Visibility(actor))
            .OrderByDescending(expense => expense.ExpenseDate).ThenBy(expense => expense.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Finds a visible expense without disclosing records outside the read scope.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The visible expense, or a 404 failure.</returns>
    public async Task<Expense> DetailAsync(ClaimsPrincipal actor, Guid id, CancellationToken cancellationToken) =>
        await _database.Expenses.AsNoTracking().Where(ExpenseAccessRules.Visibility(actor))
            .SingleOrDefaultAsync(expense => expense.Id == id, cancellationToken)
        ?? throw new ExpenseOperationException(404, "Expense not found.");

    /// <summary>Approves a submitted request and saves its audit entry.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The approved expense.</returns>
    public async Task<Expense> ApproveAsync(ClaimsPrincipal actor, Guid id, CancellationToken cancellationToken)
    {
        ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Approver);
        Expense expense = await FindForTransitionAsync(id, cancellationToken);
        _database.ExpenseHistories.Add(ExpenseWorkflowRules.Approve(expense, actor, _clock.GetUtcNow()));
        await SaveAsync(cancellationToken);
        return expense;
    }

    /// <summary>Rejects a submitted request and saves its reason and audit entry.</summary>
    /// <param name="actor">Authenticated principal.</param>
    /// <param name="id">Expense identifier.</param>
    /// <param name="request">Rejection reason.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The rejected expense.</returns>
    public async Task<Expense> RejectAsync(ClaimsPrincipal actor, Guid id, RejectExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ExpenseAccessRules.RequireRole(actor, ApplicationRoles.Approver);
        ArgumentNullException.ThrowIfNull(request);
        Expense expense = await FindForTransitionAsync(id, cancellationToken);
        _database.ExpenseHistories.Add(ExpenseWorkflowRules.Reject(expense, actor, request.Justification, _clock.GetUtcNow()));
        await SaveAsync(cancellationToken);
        return expense;
    }

    private async Task<Expense> FindForTransitionAsync(Guid id, CancellationToken cancellationToken) =>
        await _database.Expenses.SingleOrDefaultAsync(expense => expense.Id == id, cancellationToken)
        ?? throw new ExpenseOperationException(404, "Expense not found.");

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
