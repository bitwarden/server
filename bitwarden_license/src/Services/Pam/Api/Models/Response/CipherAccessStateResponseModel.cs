using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// A single-snapshot read of the caller's access state for one cipher, powering the cipher-view banner and the
/// vault-row badge. At most one of the three branches is meaningfully "next": an active lease authorizes access, a
/// pending request awaits a decision, and an approved request awaits activation by the caller.
/// </summary>
public class CipherAccessStateResponseModel : ResponseModel
{
    public CipherAccessStateResponseModel()
        : base("cipherAccessState")
    {
    }

    public Guid CipherId { get; set; }

    public AccessLeaseResponseModel? ActiveLease { get; set; }
    public AccessRequestDetailsResponseModel? PendingRequest { get; set; }

    /// <summary>
    /// An approved request awaiting activation, with a window that can still produce access. The caller activates it
    /// to mint the lease; lapsed approvals are never surfaced here.
    /// </summary>
    public AccessRequestDetailsResponseModel? ApprovedRequest { get; set; }

    /// <summary>Whether the active lease can still be extended (the rule opts in and it has not been extended yet).</summary>
    public bool ExtensionsAllowed { get; set; }

    /// <summary>The longest a single extension of the active lease may run, in seconds; null when there is no cap or no active lease.</summary>
    public int? MaxExtensionDurationSeconds { get; set; }
}
