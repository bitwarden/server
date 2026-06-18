using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// The caller's access state for a single cipher: the active lease they hold (if any), their pending request (if
/// any), and their approved-but-not-yet-activated request (if any). Approval no longer mints the lease, so the
/// approved request is the startable state in between — the caller activates it to produce the active lease.
/// ExtensionsAllowed says whether the active lease can still be extended (the rule opts in and it has not been
/// extended yet — a lease may be extended once); MaxExtensionDurationSeconds is the longest a single extension may
/// run, so the banner can gate its "Extend" control and cap the duration picker.
/// </summary>
public record CipherAccessState(
    Guid CipherId,
    AccessLease? ActiveLease,
    AccessRequestDetails? PendingRequest,
    AccessRequestDetails? ApprovedRequest,
    bool ExtensionsAllowed = false,
    int? MaxExtensionDurationSeconds = null);
