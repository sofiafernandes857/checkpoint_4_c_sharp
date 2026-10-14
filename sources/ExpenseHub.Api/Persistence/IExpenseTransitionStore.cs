using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Persistence;

/// <summary>Atomic persistence boundary for expense decisions, payments and audit reads.</summary>
public interface IExpenseTransitionStore
{
    /// <summary>Loads an isolated expense snapshot for a transition.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The snapshot, or null.</returns>
    Task<Expense?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Commits a transition and its associated records atomically, checking the original version.</summary>
    /// <param name="expense">Changed snapshot.</param>
    /// <param name="originalVersion">Version observed before changing the snapshot.</param>
    /// <param name="history">Audit entry.</param>
    /// <param name="payment">Payment for a payment transition, otherwise null.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Completion of the atomic save.</returns>
    Task CommitAsync(Expense expense, Guid originalVersion, ExpenseHistory history, PaymentRecord? payment,
        CancellationToken cancellationToken);

    /// <summary>Reads history only when the expense matches the shared visibility predicate.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="visibility">Role and ownership scope.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Visible history, or null for a missing or invisible expense.</returns>
    Task<IReadOnlyList<ExpenseHistory>?> HistoryAsync(Guid id, Expression<Func<Expense, bool>> visibility,
        CancellationToken cancellationToken);
}
