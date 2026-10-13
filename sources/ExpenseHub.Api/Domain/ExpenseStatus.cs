namespace ExpenseHub.Api.Domain;

/// <summary>Supported reimbursement states.</summary>
public enum ExpenseStatus
{
    /// <summary>An editable request.</summary>
    Draft,
    /// <summary>A request awaiting approval.</summary>
    Submitted,
    /// <summary>A request eligible for payment.</summary>
    Approved,
    /// <summary>A rejected, final request.</summary>
    Rejected,
    /// <summary>A paid, final request.</summary>
    Paid,
}
