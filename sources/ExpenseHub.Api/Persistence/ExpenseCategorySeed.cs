using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence;

/// <summary>Initial categories with stable identifiers, without additional users.</summary>
public static class ExpenseCategorySeed
{
    /// <summary>Adds missing initial categories without overwriting existing ones.</summary>
    /// <param name="database">Scoped persistence.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The asynchronous initialization.</returns>
    public static async Task InitializeAsync(ExpenseHubDbContext database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ExpenseCategory[] categories =
        {
            new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Alimentação" },
            new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Transporte" },
            new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Hospedagem" },
        };

        foreach (ExpenseCategory category in categories)
        {
            if (!await database.ExpenseCategories.AnyAsync(
                existing => existing.Id == category.Id || existing.Name == category.Name, cancellationToken))
            {
                database.ExpenseCategories.Add(category);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
