using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Services;

namespace ExpenseHub.UnitTests;

internal sealed class MemoryTransitionStore : IExpenseTransitionStore
{
    public Expense Persisted { get; set; } = new() { OwnerId = "owner", Status = ExpenseStatus.Approved };

    public List<ExpenseHistory> History { get; } = new();

    public List<PaymentRecord> Payments { get; } = new();

    public bool FailCommit { get; set; }

    public bool ConcurrentChange { get; set; }

    public int CommitAttempts { get; private set; }

    public Task<Expense?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        Expense? snapshot = id == Persisted.Id ? Clone(Persisted) : null;
        if (ConcurrentChange)
        {
            Persisted.Version = Guid.NewGuid();
        }

        return Task.FromResult(snapshot);
    }

    public Task CommitAsync(Expense expense, Guid originalVersion, ExpenseHistory history, PaymentRecord? payment,
        CancellationToken cancellationToken)
    {
        CommitAttempts++;
        if (FailCommit)
        {
            throw new IOException("Simulated atomic persistence failure.");
        }

        if (originalVersion != Persisted.Version || (payment is not null && Payments.Any(item => item.ExpenseId == expense.Id)))
        {
            throw new ExpenseOperationException(409, "Concurrent update or duplicate payment.");
        }

        Persisted = Clone(expense);
        History.Add(history);
        if (payment is not null)
        {
            Payments.Add(payment);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExpenseHistory>?> HistoryAsync(Guid id, Expression<Func<Expense, bool>> visibility,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseHistory>? result = id == Persisted.Id && visibility.Compile()(Persisted)
            ? History.Where(entry => entry.ExpenseId == id).OrderBy(entry => entry.OccurredAtUtc).ToList()
            : null;
        return Task.FromResult(result);
    }

    private static Expense Clone(Expense expense) => JsonSerializer.Deserialize<Expense>(JsonSerializer.Serialize(expense))!;
}
