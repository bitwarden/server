using Bit.Commercial.Pam.Enums;
using Bit.Commercial.Pam.Models;
using Bit.Core.Models.Api;

namespace Bit.Commercial.Pam.Api.Models.Response;

public class AccessRequestResultResponseModel : ResponseModel
{
    public AccessRequestResultResponseModel(AccessRequestResult result)
        : base("accessRequestResult")
    {
        ArgumentNullException.ThrowIfNull(result);

        ApprovalMode = result.ApprovalMode;
        Request = new AccessRequestResponseModel(result.Request);
    }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when the <see cref="Request"/> was approved on submit and is ready
    /// to activate (the client shows "Start lease"), <see cref="AccessApprovalMode.Human"/> when it is pending an
    /// approver. No lease is minted at submit on either path; the requester activates the request to start the lease.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; }

    public AccessRequestResponseModel Request { get; }
}
