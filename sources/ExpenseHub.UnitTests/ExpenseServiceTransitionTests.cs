using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Real service orchestration against an isolated atomic store, without a database provider.</summary>
[TestClass]
public sealed class ExpenseServiceTransitionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Repeated payment never reaches persistence a second time.</summary>
    [TestMethod]
    public async Task PaymentIsUniqueAndAuditableAsync()
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new();
        ExpenseService service = new(context, new FixedClock(), store);
        Guid originalVersion = store.Persisted.Version;
        Expense paid = await service.PayAsync(Actor("finance", ApplicationRoles.Finance), store.Persisted.Id, CancellationToken.None);
        Assert.AreEqual(ExpenseStatus.Paid, store.Persisted.Status);
        Assert.AreNotEqual(originalVersion, paid.Version);
        Assert.HasCount(1, store.Payments);
        Assert.HasCount(1, store.History);
        Assert.AreEqual("finance", store.Payments[0].ActorId);
        Assert.AreEqual(_now, store.Payments[0].PaidAtUtc);
        Assert.AreEqual(store.Payments[0].ExpenseId, store.History[0].ExpenseId);
        Assert.AreEqual(store.Payments[0].PaidAtUtc, store.History[0].OccurredAtUtc);
        Assert.AreEqual(409, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.PayAsync(Actor("finance", ApplicationRoles.Finance), paid.Id, CancellationToken.None))).StatusCode);
        Assert.AreEqual(1, store.CommitAttempts);
        Assert.HasCount(1, store.Payments);
        Assert.HasCount(1, store.History);
    }

    /// <summary>A logical write failure leaves all persisted data unchanged and permits a clean retry.</summary>
    [TestMethod]
    public async Task FailedPaymentDoesNotLeavePartialStateAsync()
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new() { FailCommit = true };
        ExpenseService service = new(context, new FixedClock(), store);
        Guid version = store.Persisted.Version;
        await Assert.ThrowsExactlyAsync<IOException>(
            () => service.PayAsync(Actor("finance", ApplicationRoles.Finance), store.Persisted.Id, CancellationToken.None));
        Assert.AreEqual(ExpenseStatus.Approved, store.Persisted.Status);
        Assert.AreEqual(version, store.Persisted.Version);
        Assert.IsEmpty(store.Payments);
        Assert.IsEmpty(store.History);
        store.FailCommit = false;
        await service.PayAsync(Actor("finance", ApplicationRoles.Finance), store.Persisted.Id, CancellationToken.None);
        Assert.HasCount(1, store.Payments);
        Assert.HasCount(1, store.History);
    }

    /// <summary>The original version, rather than the renewed token, protects a stale payment.</summary>
    [TestMethod]
    public async Task ConcurrentPaymentConflictsWithoutAuditAsync()
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new() { ConcurrentChange = true };
        ExpenseService service = new(context, new FixedClock(), store);
        Assert.AreEqual(409, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.PayAsync(Actor("finance", ApplicationRoles.Finance), store.Persisted.Id, CancellationToken.None))).StatusCode);
        Assert.AreEqual(ExpenseStatus.Approved, store.Persisted.Status);
        Assert.IsEmpty(store.Payments);
        Assert.IsEmpty(store.History);
    }

    /// <summary>Both decisions persist once and reject repetition without an extra history entry.</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DecisionPersistsExactlyOnceAsync(bool reject)
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new();
        store.Persisted.Status = ExpenseStatus.Submitted;
        ExpenseService service = new(context, new FixedClock(), store);
        ClaimsPrincipal actor = Actor("approver", ApplicationRoles.Approver);
        Task<Expense> Decide() => reject
            ? service.RejectAsync(actor, store.Persisted.Id, new RejectExpenseRequest { Justification = "  Reason for rejection  " }, CancellationToken.None)
            : service.ApproveAsync(actor, store.Persisted.Id, CancellationToken.None);
        await Decide();
        Assert.AreEqual(reject ? ExpenseStatus.Rejected : ExpenseStatus.Approved, store.Persisted.Status);
        Assert.HasCount(1, store.History);
        Assert.AreEqual(reject ? "Reason for rejection" : null, store.History[0].Justification);
        Assert.AreEqual("approver", store.History[0].ActorId);
        Assert.AreEqual(_now, store.History[0].OccurredAtUtc);
        Assert.AreEqual(409, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(Decide)).StatusCode);
        Assert.AreEqual(1, store.CommitAttempts);
        Assert.HasCount(1, store.History);
        Assert.IsEmpty(store.Payments);
    }

    /// <summary>A failed decision does not commit status or audit.</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedDecisionLeavesSubmittedStateAsync(bool reject)
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new() { FailCommit = true };
        store.Persisted.Status = ExpenseStatus.Submitted;
        Guid version = store.Persisted.Version;
        ExpenseService service = new(context, new FixedClock(), store);
        await Assert.ThrowsExactlyAsync<IOException>(() => reject
            ? service.RejectAsync(Actor("approver", ApplicationRoles.Approver), store.Persisted.Id,
                new RejectExpenseRequest { Justification = "Valid rejection reason" }, CancellationToken.None)
            : service.ApproveAsync(Actor("approver", ApplicationRoles.Approver), store.Persisted.Id, CancellationToken.None));
        Assert.AreEqual(ExpenseStatus.Submitted, store.Persisted.Status);
        Assert.AreEqual(version, store.Persisted.Version);
        Assert.IsEmpty(store.History);
    }

    /// <summary>Every profile receives precisely the same history visibility as expense detail.</summary>
    [TestMethod]
    public async Task HistoryUsesRoleUnionAndOwnershipForEveryStateAsync()
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new();
        ExpenseService service = new(context, new FixedClock(), store);
        store.History.Add(new ExpenseHistory { ExpenseId = store.Persisted.Id, Action = "Created" });
        foreach (ExpenseStatus state in Enum.GetValues<ExpenseStatus>())
        {
            store.Persisted.Status = state;
            foreach ((ClaimsPrincipal actor, bool visible) in new[]
            {
                (Actor("owner", ApplicationRoles.Employee), true),
                (Actor("other", ApplicationRoles.Employee), false),
                (Actor("approver", ApplicationRoles.Approver), state == ExpenseStatus.Submitted),
                (Actor("finance", ApplicationRoles.Finance), state is ExpenseStatus.Approved or ExpenseStatus.Paid),
                (Actor("auditor", ApplicationRoles.Auditor), true),
                (Actor("other", ApplicationRoles.Approver, ApplicationRoles.Finance),
                    state is ExpenseStatus.Submitted or ExpenseStatus.Approved or ExpenseStatus.Paid),
            })
            {
                if (visible)
                {
                    IReadOnlyList<ExpenseHistory> history = await service.HistoryAsync(actor, store.Persisted.Id, CancellationToken.None);
                    Assert.HasCount(1, history);
                }
                else
                {
                    Assert.AreEqual(404, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
                        () => service.HistoryAsync(actor, store.Persisted.Id, CancellationToken.None))).StatusCode);
                }
            }
        }

        Assert.AreEqual(403, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.HistoryAsync(Actor("admin", ApplicationRoles.Admin), store.Persisted.Id, CancellationToken.None))).StatusCode);
        Assert.AreEqual(401, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.HistoryAsync(new ClaimsPrincipal(), store.Persisted.Id, CancellationToken.None))).StatusCode);
        Assert.AreEqual(404, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.HistoryAsync(Actor("auditor", ApplicationRoles.Auditor), Guid.NewGuid(), CancellationToken.None))).StatusCode);
    }

    /// <summary>Missing records return 404 for an actor authorized to operate.</summary>
    [TestMethod]
    public async Task MissingExpenseCannotBeDecidedOrPaidAsync()
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new();
        ExpenseService service = new(context, new FixedClock(), store);
        Guid missing = Guid.NewGuid();
        Assert.AreEqual(404, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.ApproveAsync(Actor("approver", ApplicationRoles.Approver), missing, CancellationToken.None))).StatusCode);
        Assert.AreEqual(404, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.PayAsync(Actor("finance", ApplicationRoles.Finance), missing, CancellationToken.None))).StatusCode);
        Assert.AreEqual(0, store.CommitAttempts);
    }

    /// <summary>Each role alone lacks permission for operations assigned to other roles.</summary>
    [TestMethod]
    public async Task ServiceEnforcesRolesAndAuthenticationWithoutControllerAsync()
    {
        using ExpenseHubDbContext context = Context();
        MemoryTransitionStore store = new();
        ExpenseService service = new(context, new FixedClock(), store);
        foreach (string role in new[]
        {
            ApplicationRoles.Admin, ApplicationRoles.Employee, ApplicationRoles.Auditor,
            ApplicationRoles.Approver, ApplicationRoles.Finance,
        })
        {
            ClaimsPrincipal actor = Actor("other", role);
            if (role != ApplicationRoles.Finance)
            {
                Assert.AreEqual(403, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
                    () => service.PayAsync(actor, store.Persisted.Id, CancellationToken.None))).StatusCode);
            }

            if (role != ApplicationRoles.Approver)
            {
                Assert.AreEqual(403, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
                    () => service.ApproveAsync(actor, store.Persisted.Id, CancellationToken.None))).StatusCode);
                Assert.AreEqual(403, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
                    () => service.RejectAsync(actor, store.Persisted.Id, new RejectExpenseRequest(), CancellationToken.None))).StatusCode);
            }
        }

        Assert.AreEqual(401, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.PayAsync(new ClaimsPrincipal(), store.Persisted.Id, CancellationToken.None))).StatusCode);
        Assert.AreEqual(401, (await Assert.ThrowsExactlyAsync<ExpenseOperationException>(
            () => service.ApproveAsync(new ClaimsPrincipal(), store.Persisted.Id, CancellationToken.None))).StatusCode);
        Assert.AreEqual(0, store.CommitAttempts);
    }

    private static ExpenseHubDbContext Context() => new(new DbContextOptionsBuilder<ExpenseHubDbContext>().Options);

    private static ClaimsPrincipal Actor(string id, params string[] roles) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, id) }.Concat(roles.Select(role => new Claim(ClaimTypes.Role, role))), "test"));

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
