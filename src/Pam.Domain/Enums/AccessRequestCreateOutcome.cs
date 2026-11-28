namespace Bit.Pam.Enums;

/// <summary>The result of a race-safe request submit; each value is the code the stored procedure returns.</summary>
public enum AccessRequestCreateOutcome
{
    Created = 1,

    /// <summary>The requester already has a pending request for the item. Nothing is persisted.</summary>
    PendingRequestExists = 0,

    /// <summary>The requester already holds a live lease on the item. Nothing is persisted.</summary>
    ActiveLeaseExists = -1,

    /// <summary>The requester already has an approved request for the item that is not yet activated. Nothing is persisted.</summary>
    ApprovedRequestExists = -2,
}
