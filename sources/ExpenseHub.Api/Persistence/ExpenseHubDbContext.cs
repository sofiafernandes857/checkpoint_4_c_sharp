using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence;

/// <summary>Relational persistence for reimbursements.</summary>
public sealed class ExpenseHubDbContext : DbContext
{
    /// <summary>Initializes the context with provider options.</summary>
    /// <param name="options">Configured database options.</param>
    public ExpenseHubDbContext(DbContextOptions<ExpenseHubDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets persisted expenses.</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>Gets expense categories.</summary>
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    /// <summary>Gets immutable history entries.</summary>
    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    /// <summary>Gets simulated payment records.</summary>
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(expense => expense.Id);
            entity.Property(expense => expense.OwnerId).HasMaxLength(450).IsRequired();
            entity.Property(expense => expense.Description).HasMaxLength(500).IsRequired();
            entity.Property(expense => expense.Amount).HasPrecision(18, 2);
            entity.Property(expense => expense.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(expense => expense.Version).IsConcurrencyToken();
            entity.HasIndex(expense => new { expense.OwnerId, expense.Status });
            entity.HasIndex(expense => expense.Status);
            entity.HasOne<ExpenseCategory>().WithMany().HasForeignKey(expense => expense.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<ExpenseHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Action).HasMaxLength(50).IsRequired();
            entity.Property(history => history.ActorId).HasMaxLength(450).IsRequired();
            entity.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(history => history.NewStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(history => history.Justification).HasMaxLength(500);
            entity.Property(history => history.Changes).HasMaxLength(2000);
            entity.HasIndex(history => history.ExpenseId);
            entity.HasOne<Expense>().WithMany().HasForeignKey(history => history.ExpenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.ActorId).HasMaxLength(450).IsRequired();
            entity.HasIndex(payment => payment.ExpenseId).IsUnique();
            entity.HasOne<Expense>().WithOne().HasForeignKey<PaymentRecord>(payment => payment.ExpenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
