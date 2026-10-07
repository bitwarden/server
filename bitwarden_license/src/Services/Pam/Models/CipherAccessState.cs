using Bit.Pam.Entities;

using Bit.Pam.Models;
namespace Bit.Services.Pam.Models;

/// <summary>The caller's access state for a single cipher as of <paramref name="AsOf"/>.</summary>
/// <param name="ApprovedRequest">An approved request the caller has not yet activated into a lease.</param>
/// <param name="ExtensionsAllowed">Whether the active lease can still be extended.</param>
public record CipherAccessState(
    Guid CipherId,
    DateTime AsOf,
    AccessLease? ActiveLease,
    AccessRequestDetails? PendingRequest,
    AccessRequestDetails? ApprovedRequest,
    bool ExtensionsAllowed = false,
    int? MaxExtensionDurationSeconds = null);
