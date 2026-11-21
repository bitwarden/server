namespace Bit.Pam.Enums;

/// <summary>
/// How a request ended, as the approver history lists it. The history read derives it; nothing stores it.
/// </summary>
public enum AccessRequestHistoryStatus : byte
{
    /// <summary>An approver denied the request, or withdrew an approval before it was started.</summary>
    Denied = 0,

    /// <summary>An approver ended the lease early.</summary>
    Revoked = 1,

    /// <summary>The requester withdrew the request.</summary>
    Canceled = 2,

    /// <summary>The window closed with the request unanswered, or approved and never started.</summary>
    Expired = 3,

    /// <summary>The lease ran to its end, or its holder ended it.</summary>
    SessionEnded = 4,
}
