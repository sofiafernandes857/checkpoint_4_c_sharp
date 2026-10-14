using System;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Payment transitions and audit consistency without external services.</summary>
[TestClass]
public sealed class ExpensePaymentTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>Payment links actor, expense, UTC time and transition.</summary>
    [TestMethod]
    public void ApprovedExpenseProducesPaymentAndHistory()
    {
        Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Approved };
        Guid version = expense.Version;
        (ExpenseHistory history, PaymentRecord payment) = ExpenseWorkflowRules.Pay(expense, Finance("finance"), _now);
        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
        Assert.AreNotEqual(version, expense.Version);
        Assert.AreEqual("owner", expense.OwnerId);
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreNotEqual(Guid.Empty, payment.Id);
        Assert.AreEqual("finance", payment.ActorId);
        Assert.AreEqual(payment.ActorId, history.ActorId);
        Assert.AreEqual(_now.ToUniversalTime(), payment.PaidAtUtc);
        Assert.AreEqual(TimeSpan.Zero, payment.PaidAtUtc.Offset);
        Assert.AreEqual(payment.PaidAtUtc, history.OccurredAtUtc);
        Assert.AreEqual(payment.PaidAtUtc, expense.UpdatedAtUtc);
        Assert.AreEqual(ExpenseStatus.Approved, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, history.NewStatus);
        Assert.AreEqual("Paid", history.Action);
    }

    /// <summary>All other states, including a repeated payment, conflict without mutation.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public void InvalidPaymentStatesAreRejected(ExpenseStatus state)
    {
        Expense expense = new() { OwnerId = "owner", Status = state };
        Guid version = expense.Version;
        Assert.AreEqual(409, Assert.ThrowsExactly<ExpenseOperationException>(
            () => ExpenseWorkflowRules.Pay(expense, Finance("finance"), _now)).StatusCode);
        Assert.AreEqual(state, expense.Status);
        Assert.AreEqual(version, expense.Version);
    }

    /// <summary>Ownership blocks payment even when functional and administrative roles accumulate.</summary>
    [TestMethod]
    public void FinanceOwnerCannotPay()
    {
        Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Approved };
        Assert.AreEqual(403, Assert.ThrowsExactly<ExpenseOperationException>(
            () => ExpenseWorkflowRules.Pay(expense, Finance("owner"), _now)).StatusCode);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
    }

    private static ClaimsPrincipal Finance(string id) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id),
        new Claim(ClaimTypes.Role, ApplicationRoles.Finance),
        new Claim(ClaimTypes.Role, ApplicationRoles.Employee),
        new Claim(ClaimTypes.Role, ApplicationRoles.Approver),
        new Claim(ClaimTypes.Role, ApplicationRoles.Admin),
        new Claim(ClaimTypes.Role, ApplicationRoles.Auditor),
    }, "test"));
}
