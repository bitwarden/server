namespace Bit.Pam.Enums;

/// <summary>
/// The action taken on an <see cref="Entities.AccessRequest"/>, if any. A recorded fact rather than the current
/// status, which <see cref="AccessStatusDerivation.ComputeStatus"/> derives against the clock.
/// </summary>
public enum AccessRequestAction : byte
{
    None = 0,

    /// <summary>Approved by an approver, or by the rule engine on the automatic and extension paths.</summary>
    Approved = 1,

    /// <summary>
    /// Refused by an approver, or by the rule engine for an extension of an ended lease, or retracted by an approver
    /// before activation.
    /// </summary>
    Denied = 2,

    /// <summary>The requester withdrew their own request. Only the requester writes this.</summary>
    Cancelled = 3,
}
