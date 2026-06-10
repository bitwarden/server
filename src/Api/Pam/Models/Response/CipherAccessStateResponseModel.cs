using Bit.Core.Models.Api;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

/// <summary>
/// A single-snapshot read of the caller's access state for one cipher, powering the cipher-view banner and the
/// vault-row badge. <see cref="ApprovedRequest"/> is always null in v0: approval mints an active lease immediately,
/// so there is no approved-but-not-yet-activated request. The active lease and pending request carry the real state.
/// </summary>
public class CipherAccessStateResponseModel : ResponseModel
{
    public CipherAccessStateResponseModel(CipherAccessState state)
        : base("cipherAccessState")
    {
        ArgumentNullException.ThrowIfNull(state);

        CipherId = state.CipherId;
        ActiveLease = state.ActiveLease is null ? null : new AccessLeaseResponseModel(state.ActiveLease);
        PendingRequest = state.PendingRequest is null ? null : new AccessRequestDetailsResponseModel(state.PendingRequest);
    }

    public Guid CipherId { get; }

    public AccessLeaseResponseModel? ActiveLease { get; }
    public AccessRequestDetailsResponseModel? PendingRequest { get; }

    /// <summary>An approved request awaiting activation. Always null in v0 — approval mints the lease immediately.</summary>
    public AccessRequestDetailsResponseModel? ApprovedRequest => null;
}
