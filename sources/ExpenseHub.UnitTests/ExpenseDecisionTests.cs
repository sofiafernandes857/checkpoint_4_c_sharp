using System;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Decision rules tested independently of database and HTTP.</summary>
[TestClass]
public sealed class ExpenseDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>Approval records server metadata and preserves ownership.</summary>
    [TestMethod]
    public void ApprovalRecordsTransition()
    {
        Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Submitted };
        Guid version = expense.Version;
        ExpenseHistory history = ExpenseWorkflowRules.Approve(expense, Actor("approver"), _now);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.AreEqual("owner", expense.OwnerId);
        Assert.AreNotEqual(version, expense.Version);
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreEqual("approver", history.ActorId);
        Assert.AreEqual("Approved", history.Action);
        Assert.AreEqual(ExpenseStatus.Submitted, history.PreviousStatus);
        Assert.AreEqual(expense.Status, history.NewStatus);
        Assert.AreEqual(_now.ToUniversalTime(), history.OccurredAtUtc);
        Assert.AreEqual(TimeSpan.Zero, history.OccurredAtUtc.Offset);
        Assert.AreEqual(history.OccurredAtUtc, expense.UpdatedAtUtc);
        Assert.IsNull(history.Justification);
    }

    /// <summary>Rejection accepts both inclusive limits after trimming.</summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void RejectionRecordsTrimmedReason(int length)
    {
        Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Submitted };
        string reason = new('a', length);
        ExpenseHistory history = ExpenseWorkflowRules.Reject(expense, Actor("approver"), "  " + reason + "  ", _now);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual("Rejected", history.Action);
        Assert.AreEqual(reason, history.Justification);
        Assert.AreEqual(ExpenseStatus.Submitted, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, history.NewStatus);
    }

    /// <summary>Invalid input cannot mutate the request.</summary>
    [TestMethod]
    public void InvalidReasonsLeaveExpenseUntouched()
    {
        foreach (string? reason in new[] { null, string.Empty, "          ", " 123456789 ", new string('a', 501) })
        {
            Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Submitted };
            Guid version = expense.Version;
            ExpenseOperationException error = Assert.ThrowsExactly<ExpenseOperationException>(
                () => ExpenseWorkflowRules.Reject(expense, Actor("approver"), reason, _now));
            Assert.AreEqual(400, error.StatusCode);
            Assert.AreEqual(version, expense.Version);
            Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        }
    }

    /// <summary>Every incompatible state rejects both decisions.</summary>
    [TestMethod]
    public void InvalidStatesAndRepeatedDecisionsConflict()
    {
        foreach (ExpenseStatus state in new[] { ExpenseStatus.Draft, ExpenseStatus.Approved, ExpenseStatus.Rejected, ExpenseStatus.Paid })
        {
            Expense expense = new() { OwnerId = "owner", Status = state };
            Guid version = expense.Version;
            Assert.AreEqual(409, Assert.ThrowsExactly<ExpenseOperationException>(
                () => ExpenseWorkflowRules.Approve(expense, Actor("approver"), _now)).StatusCode);
            Assert.AreEqual(409, Assert.ThrowsExactly<ExpenseOperationException>(
                () => ExpenseWorkflowRules.Reject(expense, Actor("approver"), "Valid rejection reason", _now)).StatusCode);
            Assert.AreEqual(version, expense.Version);
            Assert.AreEqual(state, expense.Status);
        }
    }

    /// <summary>Combined roles never grant self approval or self rejection.</summary>
    [TestMethod]
    public void OwnerCannotDecideEvenWithAllRoles()
    {
        Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Submitted };
        Assert.AreEqual(403, Assert.ThrowsExactly<ExpenseOperationException>(
            () => ExpenseWorkflowRules.Approve(expense, Actor("owner"), _now)).StatusCode);
        Assert.AreEqual(403, Assert.ThrowsExactly<ExpenseOperationException>(
            () => ExpenseWorkflowRules.Reject(expense, Actor("owner"), "Valid rejection reason", _now)).StatusCode);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    private static ClaimsPrincipal Actor(string id) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id),
        new Claim(ClaimTypes.Role, ApplicationRoles.Approver),
        new Claim(ClaimTypes.Role, ApplicationRoles.Employee),
        new Claim(ClaimTypes.Role, ApplicationRoles.Finance),
        new Claim(ClaimTypes.Role, ApplicationRoles.Admin),
        new Claim(ClaimTypes.Role, ApplicationRoles.Auditor),
    }, "test"));
}
