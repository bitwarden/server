using Bit.Pam.Entities;

using Bit.Pam.Models;
namespace Bit.Services.Pam.Models;

/// <summary>
/// The caller's access state for a single cipher as of <paramref name="AsOf"/>.
/// </summary>
/// <param name="ApprovedRequest">An approved request the caller has not yet activated into a lease.</param>
/// <param name="ExtensionsAllowed">Whether the active lease can still be extended.</param>
/// <param name="MaxExtensionDurationSeconds">The longest single extension the rule allows.</param>
/// <param name="CanRequest">
/// False when nothing is held or pending and the rule's conditions would refuse a new request now.
/// </param>
public record CipherAccessState(
    Guid CipherId,
    DateTime AsOf,
    AccessLease? ActiveLease,
    AccessRequestDetails? PendingRequest,
    AccessRequestDetails? ApprovedRequest,
    bool ExtensionsAllowed = false,
    int? MaxExtensionDurationSeconds = null,
    bool CanRequest = true);
