namespace Bit.Core.PrivilegedAccessManagement.Models.Policies;

/// <summary>
/// Always requires a human decision before a lease can be issued.
/// </summary>
public sealed class HumanApprovalPolicy : LeasingPolicy;
