namespace Bit.Services.Pam.Models;

/// <summary>
/// The facts behind a gated cipher's badge, for the batch read. Unlike <see cref="CipherAccessState"/>, it carries
/// no request details or extension terms.
/// </summary>
/// <param name="ActiveLeaseNotAfter">When the caller's active lease ends; null without one.</param>
/// <param name="HasApprovedRequest">The caller holds an approved request not yet activated into a lease.</param>
/// <param name="HasPendingRequest">The caller has a request awaiting a decision.</param>
/// <param name="CanRequest">
/// False when nothing is held or pending and the rule's conditions would refuse a new request now.
/// </param>
public record CipherAccessSummary(
    Guid CipherId,
    DateTime? ActiveLeaseNotAfter,
    bool HasApprovedRequest,
    bool HasPendingRequest,
    bool CanRequest);
