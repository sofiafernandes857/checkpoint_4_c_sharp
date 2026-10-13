using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Security;

/// <summary>Roles accepted by the application.</summary>
public static class ApplicationRoles
{
    /// <summary>User administration.</summary>
    public const string Admin = "Admin";
    /// <summary>Expense ownership.</summary>
    public const string Employee = "Employee";
    /// <summary>Expense decisions.</summary>
    public const string Approver = "Approver";
    /// <summary>Simulated payments.</summary>
    public const string Finance = "Finance";
    /// <summary>Read-only audit access.</summary>
    public const string Auditor = "Auditor";

    /// <summary>Gets the complete, immutable list of supported roles.</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
        new[] { Admin, Employee, Approver, Finance, Auditor });
}
