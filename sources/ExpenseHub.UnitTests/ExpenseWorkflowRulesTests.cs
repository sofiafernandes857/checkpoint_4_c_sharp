using System;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Submission transitions and negative cases without persistence.</summary>
[TestClass]
public sealed class ExpenseWorkflowRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Submission renews version and records the exact transition.</summary>
    [TestMethod]
    public void OwnedDraftIsSubmittedWithHistory()
    {
        Expense expense = new() { OwnerId = "owner", CreatedAtUtc = _now.AddHours(-1) };
        Guid version = expense.Version;
        ExpenseHistory history = ExpenseWorkflowRules.Submit(expense, Employee("owner"), _now);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.AreNotEqual(version, expense.Version);
        Assert.AreEqual(_now, expense.UpdatedAtUtc);
        Assert.AreEqual(_now.AddHours(-1), expense.CreatedAtUtc);
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreEqual("owner", history.ActorId);
        Assert.AreEqual("Submitted", history.Action);
        Assert.AreEqual(ExpenseStatus.Draft, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Submitted, history.NewStatus);
        Assert.AreEqual(_now, history.OccurredAtUtc);
    }

    /// <summary>A second submission cannot mutate or produce a second entry.</summary>
    [TestMethod]
    public void RepeatedSubmissionReturnsConflictWithoutMutation()
    {
        Expense expense = new() { OwnerId = "owner" };
        ExpenseWorkflowRules.Submit(expense, Employee("owner"), _now);
        Guid version = expense.Version;
        AssertStatus(409, () => ExpenseWorkflowRules.Submit(expense, Employee("owner"), _now.AddHours(1)));
        Assert.AreEqual(version, expense.Version);
        Assert.AreEqual(_now, expense.UpdatedAtUtc);
    }

    /// <summary>Each state other than Draft rejects submission.</summary>
    [TestMethod]
    public void NonDraftStatesRejectSubmission()
    {
        foreach (ExpenseStatus state in new[]
        {
            ExpenseStatus.Submitted, ExpenseStatus.Approved, ExpenseStatus.Rejected, ExpenseStatus.Paid,
        })
        {
            Expense expense = new() { OwnerId = "owner", Status = state };
            AssertStatus(409, () => ExpenseWorkflowRules.Submit(expense, Employee("owner"), _now));
            Assert.AreEqual(state, expense.Status);
        }
    }

    /// <summary>Another employee cannot discover or submit the owned expense.</summary>
    [TestMethod]
    public void AnotherOwnerReturnsNotFound()
    {
        Expense expense = new() { OwnerId = "owner" };
        AssertStatus(404, () => ExpenseWorkflowRules.Submit(expense, Employee("other"), _now));
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    /// <summary>Authentication and Employee are required even without controller middleware.</summary>
    [TestMethod]
    public void SubmissionEnforcesAuthenticationAndRole()
    {
        Expense expense = new() { OwnerId = "owner" };
        AssertStatus(401, () => ExpenseWorkflowRules.Submit(expense, new ClaimsPrincipal(), _now));
        ClaimsPrincipal admin = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "owner"), new Claim(ClaimTypes.Role, ApplicationRoles.Admin),
        }, "test"));
        AssertStatus(403, () => ExpenseWorkflowRules.Submit(expense, admin, _now));
    }

    private static ClaimsPrincipal Employee(string id) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, ApplicationRoles.Employee),
    }, "test"));

    private static void AssertStatus(int expected, Action action)
    {
        ExpenseOperationException exception = Assert.ThrowsExactly<ExpenseOperationException>(action);
        Assert.AreEqual(expected, exception.StatusCode);
    }
}
