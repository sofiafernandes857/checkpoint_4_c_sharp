using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence;

/// <summary>Persists each transition in one relational SaveChanges transaction.</summary>
public sealed class ExpenseTransitionStore : IExpenseTransitionStore
{
    private readonly ExpenseHubDbContext _database;

    /// <summary>Initializes the scoped persistence adapter.</summary>
    /// <param name="database">Shared application context.</param>
    public ExpenseTransitionStore(ExpenseHubDbContext database)
    {
        _database = database;
    }

    /// <inheritdoc/>
    public Task<Expense?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _database.Expenses.AsNoTracking().SingleOrDefaultAsync(expense => expense.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task CommitAsync(Expense expense, Guid originalVersion, ExpenseHistory history, PaymentRecord? payment,
        CancellationToken cancellationToken)
    {
        try
        {
            _database.Expenses.Update(expense);
            _database.Entry(expense).Property(item => item.Version).OriginalValue = originalVersion;
            _database.ExpenseHistories.Add(history);
            if (payment is not null)
            {
                _database.PaymentRecords.Add(payment);
            }

            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ExpenseOperationException(409, "The expense changed concurrently. Reload and retry.");
        }
        catch (DbUpdateException error) when (payment is not null &&
            error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            throw new ExpenseOperationException(409, "A payment already exists for this expense.");
        }
        finally
        {
            _database.ChangeTracker.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExpenseHistory>?> HistoryAsync(Guid id, Expression<Func<Expense, bool>> visibility,
        CancellationToken cancellationToken)
    {
        IQueryable<Expense> visible = _database.Expenses.AsNoTracking().Where(visibility).Where(expense => expense.Id == id);
        if (!await visible.AnyAsync(cancellationToken))
        {
            return null;
        }

        List<ExpenseHistory> history = await _database.ExpenseHistories.AsNoTracking()
            .Where(entry => visible.Any(expense => expense.Id == entry.ExpenseId)).ToListAsync(cancellationToken);
        // SQLite cannot order DateTimeOffset directly; authorization and filtering occur in SQL first.
        return history.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id).ToList();
    }
}
