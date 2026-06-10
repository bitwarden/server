using Bit.Core.Models.Api;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

public class AccessRequestResultResponseModel : ResponseModel
{
    public AccessRequestResultResponseModel(AccessRequestResult result)
        : base("accessRequestResult")
    {
        ArgumentNullException.ThrowIfNull(result);

        ApprovalMode = result.ApprovalMode == AccessApprovalMode.Human ? "human" : "automatic";
        Lease = result.Lease is null ? null : new AccessLeaseResponseModel(result.Lease);
        Request = result.Request is null ? null : new AccessRequestResponseModel(result.Request);
    }

    /// <summary>
    /// <c>"automatic"</c> when a <see cref="Lease"/> was issued immediately, <c>"human"</c> when a pending
    /// <see cref="Request"/> was created.
    /// </summary>
    public string ApprovalMode { get; }

    public AccessLeaseResponseModel? Lease { get; }
    public AccessRequestResponseModel? Request { get; }
}
