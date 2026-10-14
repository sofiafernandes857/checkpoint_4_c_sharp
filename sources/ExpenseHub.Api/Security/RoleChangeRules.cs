using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpenseHub.Api.Security;

/// <summary>Pure validation rules for administrative role changes.</summary>
public static class RoleChangeRules
{
    /// <summary>Validates supported roles and prevents self-removal of Admin.</summary>
    /// <param name="actorId">Authenticated administrator identifier.</param>
    /// <param name="targetId">Target user identifier.</param>
    /// <param name="requestedRoles">Desired complete role set.</param>
    /// <returns>An error message, or null for a valid request.</returns>
    public static string? GetError(string actorId, string targetId, IReadOnlyCollection<string> requestedRoles)
    {
        ArgumentNullException.ThrowIfNull(requestedRoles);

        if (requestedRoles.Any(role => !ApplicationRoles.All.Contains(role, StringComparer.Ordinal)))
        {
            return "Only Admin, Employee, Approver, Finance and Auditor roles are accepted.";
        }

        if (requestedRoles.Distinct(StringComparer.Ordinal).Count() != requestedRoles.Count)
        {
            return "Duplicate roles are not accepted.";
        }

        if (string.Equals(actorId, targetId, StringComparison.Ordinal) &&
            !requestedRoles.Contains(ApplicationRoles.Admin, StringComparer.Ordinal))
        {
            return "An administrator cannot remove their own Admin role.";
        }

        return null;
    }
}
