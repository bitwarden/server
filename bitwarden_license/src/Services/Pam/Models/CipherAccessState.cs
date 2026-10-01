using Bit.Pam.Entities;

using Bit.Pam.Models;
namespace Bit.Services.Pam.Models;

/// <summary>
/// The caller's access state for a single cipher as of <paramref name="AsOf"/>: their active lease, pending
/// request, and approved-but-not-yet-activated request (each if any). The approved request is the startable
/// state in between — the caller activates it to produce the active lease. ExtensionsAllowed is whether the
/// active lease can still be extended; MaxExtensionDurationSeconds bounds a single extension.
/// </summary>
public record CipherAccessState(
    Guid CipherId,
    DateTime AsOf,
    AccessLease? ActiveLease,
    AccessRequestDetails? PendingRequest,
    AccessRequestDetails? ApprovedRequest,
    bool ExtensionsAllowed = false,
    int? MaxExtensionDurationSeconds = null);
