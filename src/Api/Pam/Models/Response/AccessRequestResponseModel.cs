using Bit.Core.Models.Api;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

public class AccessRequestResponseModel : ResponseModel
{
    public AccessRequestResponseModel(AccessRequestResult result)
        : base("accessRequest")
    {
        ArgumentNullException.ThrowIfNull(result);

        Outcome = result.Outcome == AccessApprovalOutcome.Human ? "human" : "automatic";
        Lease = result.Lease is null ? null : new LeaseResponseModel(result.Lease);
        Request = result.Request is null ? null : new LeaseRequestResponseModel(result.Request);
    }

    /// <summary>
    /// <c>"automatic"</c> when a <see cref="Lease"/> was issued immediately, <c>"human"</c> when a pending
    /// <see cref="Request"/> was created.
    /// </summary>
    public string Outcome { get; }

    public LeaseResponseModel? Lease { get; }
    public LeaseRequestResponseModel? Request { get; }
}
