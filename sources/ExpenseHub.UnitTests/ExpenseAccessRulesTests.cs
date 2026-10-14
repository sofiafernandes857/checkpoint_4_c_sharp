using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Role combinations, contextual authorization and visibility without persistence.</summary>
[TestClass]
public sealed class ExpenseAccessRulesTests
{
    /// <summary>Every combination of five roles follows the union of functional reading permissions.</summary>
    [TestMethod]
    public void AllRoleCombinationsRespectVisibilityForEveryStateAndOwner()
    {
        for (int mask = 0; mask < 32; mask++)
        {
            string[] roles = Roles(mask);
            ClaimsPrincipal actor = Principal("actor", roles);
            bool employee = roles.Contains(ApplicationRoles.Employee);
            bool approver = roles.Contains(ApplicationRoles.Approver);
            bool finance = roles.Contains(ApplicationRoles.Finance);
            bool auditor = roles.Contains(ApplicationRoles.Auditor);

            if (!employee && !approver && !finance && !auditor)
            {
                AssertStatus(403, () => ExpenseAccessRules.Visibility(actor));
                continue;
            }

            Func<Expense, bool> visible = ExpenseAccessRules.Visibility(actor).Compile();

            foreach (ExpenseStatus state in Enum.GetValues<ExpenseStatus>())
            {
                foreach (string owner in new[] { "actor", "other" })
                {
                    bool expected = auditor || (employee && owner == "actor") ||
                        (approver && state == ExpenseStatus.Submitted) ||
                        (finance && (state == ExpenseStatus.Approved || state == ExpenseStatus.Paid));
                    Assert.AreEqual(expected, visible(new Expense { OwnerId = owner, Status = state }),
                        $"Roles mask {mask}, state {state}, owner {owner}.");
                }
            }
        }
    }

    /// <summary>Every role combination preserves self-decision and self-payment prohibitions.</summary>
    [TestMethod]
    public void AccumulatingRolesNeverAllowsSelfDecisionOrPayment()
    {
        for (int mask = 0; mask < 32; mask++)
        {
            ClaimsPrincipal actor = Principal("owner", Roles(mask));
            Expense expense = new() { OwnerId = "owner", Status = ExpenseStatus.Submitted };
            AssertStatus(403, () => ExpenseAccessRules.RequireDecision(expense, actor));
            expense.Status = ExpenseStatus.Approved;
            AssertStatus(403, () => ExpenseAccessRules.RequirePayment(expense, actor));
        }
    }

    /// <summary>Decision permission requires Approver and exactly Submitted for a different owner.</summary>
    [TestMethod]
    public void DecisionsRequireRoleAndSubmittedState()
    {
        Expense expense = new() { OwnerId = "other", Status = ExpenseStatus.Submitted };
        Assert.AreEqual("actor", ExpenseAccessRules.RequireDecision(expense, Principal("actor", ApplicationRoles.Approver)));
        AssertStatus(403, () => ExpenseAccessRules.RequireDecision(expense, Principal("actor", ApplicationRoles.Finance)));
        AssertStatus(403, () => ExpenseAccessRules.RequireDecision(expense, Principal("actor", ApplicationRoles.Admin)));

        foreach (ExpenseStatus state in Enum.GetValues<ExpenseStatus>().Where(state => state != ExpenseStatus.Submitted))
        {
            expense.Status = state;
            AssertStatus(409, () => ExpenseAccessRules.RequireDecision(expense, Principal("actor", ApplicationRoles.Approver)));
            Assert.AreEqual(state, expense.Status);
        }
    }

    /// <summary>Payment permission requires Finance and exactly Approved for a different owner.</summary>
    [TestMethod]
    public void PaymentsRequireRoleAndApprovedState()
    {
        Expense expense = new() { OwnerId = "other", Status = ExpenseStatus.Approved };
        Assert.AreEqual("actor", ExpenseAccessRules.RequirePayment(expense, Principal("actor", ApplicationRoles.Finance)));
        AssertStatus(403, () => ExpenseAccessRules.RequirePayment(expense, Principal("actor", ApplicationRoles.Approver)));
        AssertStatus(403, () => ExpenseAccessRules.RequirePayment(expense, Principal("actor", ApplicationRoles.Admin)));

        foreach (ExpenseStatus state in Enum.GetValues<ExpenseStatus>().Where(state => state != ExpenseStatus.Approved))
        {
            expense.Status = state;
            AssertStatus(409, () => ExpenseAccessRules.RequirePayment(expense, Principal("actor", ApplicationRoles.Finance)));
            Assert.AreEqual(state, expense.Status);
        }
    }

    /// <summary>Forged claims without an authenticated identity and missing IDs are rejected.</summary>
    [TestMethod]
    public void AuthenticationAndIdentifierAreRequired()
    {
        ClaimsPrincipal anonymous = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Role, ApplicationRoles.Auditor),
        }));
        ClaimsPrincipal missingId = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, ApplicationRoles.Auditor),
        }, "test"));
        AssertStatus(401, () => ExpenseAccessRules.Visibility(anonymous));
        AssertStatus(401, () => ExpenseAccessRules.Visibility(missingId));
        AssertStatus(401, () => ExpenseAccessRules.RequireRole(anonymous, ApplicationRoles.Employee));
        AssertStatus(401, () => ExpenseAccessRules.RequireDecision(new Expense(), anonymous));
        AssertStatus(401, () => ExpenseAccessRules.RequirePayment(new Expense(), missingId));
    }

    /// <summary>Ownership comparison is exact, not case insensitive.</summary>
    [TestMethod]
    public void OwnerIdentifiersUseOrdinalMatching()
    {
        Expense expense = new() { OwnerId = "Owner", Status = ExpenseStatus.Draft };
        AssertStatus(404, () => ExpenseAccessRules.RequireOwnedDraft(expense, "owner"));
        Assert.IsFalse(ExpenseAccessRules.Visibility(Principal("owner", ApplicationRoles.Employee)).Compile()(expense));
    }

    /// <summary>Auditor visibility and combined roles never grant unrelated write permissions.</summary>
    [TestMethod]
    public void ReadingDoesNotGrantFunctionalWriteRoles()
    {
        ClaimsPrincipal auditor = Principal("actor", ApplicationRoles.Auditor, ApplicationRoles.Admin);
        Expense expense = new() { OwnerId = "other", Status = ExpenseStatus.Submitted };
        Assert.IsTrue(ExpenseAccessRules.Visibility(auditor).Compile()(expense));
        AssertStatus(403, () => ExpenseAccessRules.RequireRole(auditor, ApplicationRoles.Employee));
        AssertStatus(403, () => ExpenseAccessRules.RequireDecision(expense, auditor));
        expense.Status = ExpenseStatus.Approved;
        AssertStatus(403, () => ExpenseAccessRules.RequirePayment(expense, auditor));
    }

    private static string[] Roles(int mask)
    {
        List<string> roles = new();

        for (int index = 0; index < ApplicationRoles.All.Count; index++)
        {
            if ((mask & (1 << index)) != 0)
            {
                roles.Add(ApplicationRoles.All[index]);
            }
        }

        return roles.ToArray();
    }

    private static ClaimsPrincipal Principal(string id, params string[] roles)
    {
        List<Claim> claims = new() { new Claim(ClaimTypes.NameIdentifier, id) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static void AssertStatus(int expected, Action action)
    {
        ExpenseOperationException exception = Assert.ThrowsExactly<ExpenseOperationException>(action);
        Assert.AreEqual(expected, exception.StatusCode);
    }
}
