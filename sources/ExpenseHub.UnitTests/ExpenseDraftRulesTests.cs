using System;
using System.Text.Json;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Draft business rules without database, network or real clock dependencies.</summary>
[TestClass]
public sealed class ExpenseDraftRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _categoryId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    /// <summary>Server-owned fields and initial history are consistent.</summary>
    [TestMethod]
    public void CreateUsesServerIdentityAndInstant()
    {
        ExpenseDraftRequest request = ValidRequest();
        request.Description = "  Almoço de trabalho  ";
        (Expense expense, ExpenseHistory history) = ExpenseDraftRules.Create(request, "employee", _now);
        Assert.AreEqual("employee", expense.OwnerId);
        Assert.AreEqual("Almoço de trabalho", expense.Description);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreNotEqual(Guid.Empty, expense.Id);
        Assert.AreNotEqual(Guid.Empty, expense.Version);
        Assert.AreEqual(_now, expense.CreatedAtUtc);
        Assert.AreEqual(_now, expense.UpdatedAtUtc);
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreEqual("employee", history.ActorId);
        Assert.AreEqual("Created", history.Action);
        Assert.IsNull(history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, history.NewStatus);
        Assert.AreEqual(_now, history.OccurredAtUtc);
    }

    /// <summary>Description boundaries are measured after trimming.</summary>
    [TestMethod]
    public void DescriptionRejectsWhitespaceAndInvalidLengths()
    {
        foreach (string description in new[] { "          ", " 123456789 ", new string('a', 501) })
        {
            ExpenseDraftRequest request = ValidRequest();
            request.Description = description;
            AssertStatus(400, () => ExpenseDraftRules.Validate(request, DateOnly.FromDateTime(_now.UtcDateTime)));
        }
    }

    /// <summary>Inclusive description and amount boundaries are accepted.</summary>
    [TestMethod]
    public void InclusiveBoundariesAreAccepted()
    {
        ExpenseDraftRequest request = ValidRequest();
        request.Description = new string('a', 10);
        request.Amount = 0.01m;
        ExpenseDraftRules.Validate(request, DateOnly.FromDateTime(_now.UtcDateTime));
        request.Description = new string('a', 500);
        request.Amount = int.MaxValue;
        ExpenseDraftRules.Validate(request, DateOnly.FromDateTime(_now.UtcDateTime));
    }

    /// <summary>Zero, negative and excessive amounts cannot create expenses.</summary>
    [TestMethod]
    public void InvalidAmountsAreRejected()
    {
        foreach (decimal amount in new[] { 0m, -1m, 0.009m, 2147483647.01m })
        {
            ExpenseDraftRequest request = ValidRequest();
            request.Amount = amount;
            AssertStatus(400, () => ExpenseDraftRules.Create(request, "employee", _now));
        }
    }

    /// <summary>Missing, minimum and future dates are invalid.</summary>
    [TestMethod]
    public void InvalidDatesAreRejected()
    {
        foreach (DateOnly? date in new DateOnly?[] { null, DateOnly.MinValue, new(2026, 10, 15) })
        {
            ExpenseDraftRequest request = ValidRequest();
            request.ExpenseDate = date;
            AssertStatus(400, () => ExpenseDraftRules.Create(request, "employee", _now));
        }
    }

    /// <summary>Missing and empty category identifiers are invalid.</summary>
    [TestMethod]
    public void MissingCategoryIsRejected()
    {
        ExpenseDraftRequest request = ValidRequest();
        request.CategoryId = null;
        AssertStatus(400, () => ExpenseDraftRules.Create(request, "employee", _now));
        request.CategoryId = Guid.Empty;
        AssertStatus(400, () => ExpenseDraftRules.Create(request, "employee", _now));
    }

    /// <summary>Ownership failure leaves the expense untouched.</summary>
    [TestMethod]
    public void EditingAnotherEmployeesDraftReturnsNotFound()
    {
        (Expense expense, _) = ExpenseDraftRules.Create(ValidRequest(), "owner", _now);
        Guid version = expense.Version;
        AssertStatus(404, () => ExpenseDraftRules.Edit(expense, ValidRequest(), "other", _now.AddHours(1)));
        Assert.AreEqual(version, expense.Version);
        Assert.AreEqual(_now, expense.UpdatedAtUtc);
    }

    /// <summary>Every state after Draft rejects editing.</summary>
    [TestMethod]
    public void EditingNonDraftReturnsConflict()
    {
        foreach (ExpenseStatus status in new[]
        {
            ExpenseStatus.Submitted, ExpenseStatus.Approved, ExpenseStatus.Rejected, ExpenseStatus.Paid,
        })
        {
            (Expense expense, _) = ExpenseDraftRules.Create(ValidRequest(), "owner", _now);
            expense.Status = status;
            Guid version = expense.Version;
            AssertStatus(409, () => ExpenseDraftRules.Edit(expense, ValidRequest(), "owner", _now.AddHours(1)));
            Assert.AreEqual(version, expense.Version);
            Assert.AreEqual(status, expense.Status);
        }
    }

    /// <summary>Changes are auditable and renew concurrency without replacing immutable values.</summary>
    [TestMethod]
    public void EditRecordsPreviousAndCurrentValues()
    {
        (Expense expense, _) = ExpenseDraftRules.Create(ValidRequest(), "owner", _now);
        Guid version = expense.Version;
        ExpenseDraftRequest edited = ValidRequest();
        edited.Amount = 50m;
        ExpenseHistory history = ExpenseDraftRules.Edit(expense, edited, "owner", _now.AddHours(1));
        Assert.AreEqual(50m, expense.Amount);
        Assert.AreNotEqual(version, expense.Version);
        Assert.AreEqual(_now, expense.CreatedAtUtc);
        Assert.AreEqual(_now.AddHours(1), expense.UpdatedAtUtc);
        Assert.AreEqual("owner", expense.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, history.NewStatus);
        Assert.AreEqual("Edited", history.Action);
        using JsonDocument changes = JsonDocument.Parse(history.Changes!);
        Assert.AreEqual(25m, changes.RootElement.GetProperty("Amount").GetProperty("previous").GetDecimal());
        Assert.AreEqual(50m, changes.RootElement.GetProperty("Amount").GetProperty("current").GetDecimal());
        Assert.IsFalse(changes.RootElement.TryGetProperty("Description", out _));
    }

    /// <summary>Invalid editing cannot partially change the draft.</summary>
    [TestMethod]
    public void InvalidEditDoesNotMutateExpense()
    {
        (Expense expense, _) = ExpenseDraftRules.Create(ValidRequest(), "owner", _now);
        ExpenseDraftRequest invalid = ValidRequest();
        invalid.Amount = -1m;
        Guid version = expense.Version;
        AssertStatus(400, () => ExpenseDraftRules.Edit(expense, invalid, "owner", _now.AddHours(1)));
        Assert.AreEqual(25m, expense.Amount);
        Assert.AreEqual(version, expense.Version);
    }

    /// <summary>Protected and unknown JSON fields cannot bypass the contract.</summary>
    [TestMethod]
    public void ProtectedFieldsAreRejectedByJsonContract()
    {
        foreach (string field in new[] { "ownerId", "status", "actorId", "createdAtUtc", "version", "id" })
        {
            string json = "{\"" + field + "\":\"client-value\"}";
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<ExpenseDraftRequest>(json,
                JsonSerializerOptions.Web));
        }
    }

    private static ExpenseDraftRequest ValidRequest() => new()
    {
        Description = "Almoço de trabalho",
        Amount = 25m,
        ExpenseDate = new DateOnly(2026, 10, 14),
        CategoryId = _categoryId,
    };

    private static void AssertStatus(int expected, Action action)
    {
        ExpenseOperationException exception = Assert.ThrowsExactly<ExpenseOperationException>(action);
        Assert.AreEqual(expected, exception.StatusCode);
    }
}
