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
        Request = new AccessRequestResponseModel(result.Request);
    }

    /// <summary>
    /// <c>"automatic"</c> when the <see cref="Request"/> was approved on submit and is ready to activate (the client
    /// shows "Start lease"), <c>"human"</c> when it is pending an approver. No lease is minted at submit on either
    /// path; the requester activates the request to start the lease.
    /// </summary>
    public string ApprovalMode { get; }

    public AccessRequestResponseModel Request { get; }
}
