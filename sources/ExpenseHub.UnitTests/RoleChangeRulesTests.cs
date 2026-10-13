using System;
using ExpenseHub.Api.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Role changes are validated without database or HTTP dependencies.</summary>
[TestClass]
public sealed class RoleChangeRulesTests
{
    private static readonly string[] _unknownRoles = { "SuperAdmin" };
    private static readonly string[] _incorrectCasing = { "admin" };

    /// <summary>An administrator cannot remove their own administration permission.</summary>
    [TestMethod]
    public void SelfRemovalOfAdminIsRejected()
    {
        Assert.IsNotNull(RoleChangeRules.GetError("actor", "actor", new[] { ApplicationRoles.Employee }));
    }

    /// <summary>An empty replacement does not bypass the self-removal rule.</summary>
    [TestMethod]
    public void EmptySelfAssignmentIsRejected()
    {
        Assert.IsNotNull(RoleChangeRules.GetError("actor", "actor", Array.Empty<string>()));
    }

    /// <summary>Keeping Admin while adding a role is allowed.</summary>
    [TestMethod]
    public void SelfAssignmentKeepingAdminIsAllowed()
    {
        Assert.IsNull(RoleChangeRules.GetError("actor", "actor", new[] { ApplicationRoles.Admin, ApplicationRoles.Employee }));
    }

    /// <summary>Other users may have all permissions removed.</summary>
    [TestMethod]
    public void EmptyAssignmentForAnotherUserIsAllowed()
    {
        Assert.IsNull(RoleChangeRules.GetError("actor", "other", Array.Empty<string>()));
    }

    /// <summary>A new role cannot be created by providing its name.</summary>
    [TestMethod]
    public void UnknownRoleIsRejected()
    {
        Assert.IsNotNull(RoleChangeRules.GetError("actor", "other", _unknownRoles));
    }

    /// <summary>Role matching uses exact canonical names.</summary>
    [TestMethod]
    public void IncorrectRoleCasingIsRejected()
    {
        Assert.IsNotNull(RoleChangeRules.GetError("actor", "other", _incorrectCasing));
    }

    /// <summary>Duplicate input is rejected rather than applied twice.</summary>
    [TestMethod]
    public void DuplicateRolesAreRejected()
    {
        Assert.IsNotNull(RoleChangeRules.GetError("actor", "other", new[] { ApplicationRoles.Employee, ApplicationRoles.Employee }));
    }

    /// <summary>All documented roles may be combined on another account.</summary>
    [TestMethod]
    public void KnownRolesForAnotherUserAreAllowed()
    {
        Assert.IsNull(RoleChangeRules.GetError("actor", "other", new[]
        {
            ApplicationRoles.Admin, ApplicationRoles.Employee, ApplicationRoles.Approver,
            ApplicationRoles.Finance, ApplicationRoles.Auditor,
        }));
    }
}
