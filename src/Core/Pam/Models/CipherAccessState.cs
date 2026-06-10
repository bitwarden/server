using Bit.Core.Pam.Entities;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The caller's access state for a single cipher: the active lease they hold (if any), their pending request (if
/// any), and their approved-but-not-yet-activated request (if any). Approval no longer mints the lease, so the
/// approved request is the startable state in between — the caller activates it to produce the active lease.
/// </summary>
public record CipherAccessState(
    Guid CipherId,
    AccessLease? ActiveLease,
    AccessRequestDetails? PendingRequest,
    AccessRequestDetails? ApprovedRequest);
