using Bit.Core.Models.Api;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

/// <summary>
/// A single-snapshot read of the caller's lease state for one cipher, powering the cipher-view banner and the
/// vault-row badge. <see cref="CipherLeaseSnapshot.ApprovedTicket"/> is always null in v0: approval mints an active
/// lease immediately, so there is no approved-but-unredeemed ticket to redeem. The active lease and pending request
/// carry the real state.
/// </summary>
public class CipherLeaseStateResponseModel : ResponseModel
{
    public CipherLeaseStateResponseModel(CipherLeaseStateResult result)
        : base("cipherLeaseState")
    {
        ArgumentNullException.ThrowIfNull(result);

        CipherId = result.CipherId;
        Lease = new CipherLeaseSnapshot
        {
            ActiveLease = result.ActiveLease is null ? null : new MemberLeaseResponseModel(result.ActiveLease),
            PendingRequest = result.PendingRequest is null ? null : new InboxAccessRequestResponseModel(result.PendingRequest),
            ApprovedTicket = null,
        };
    }

    public Guid CipherId { get; }
    public CipherLeaseSnapshot Lease { get; }

    public class CipherLeaseSnapshot
    {
        public MemberLeaseResponseModel? ActiveLease { get; init; }
        public InboxAccessRequestResponseModel? PendingRequest { get; init; }
        public InboxAccessRequestResponseModel? ApprovedTicket { get; init; }
    }
}
