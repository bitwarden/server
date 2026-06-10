using Bit.Core.Models.Api;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

public class AccessPreCheckResponseModel : ResponseModel
{
    public AccessPreCheckResponseModel(Guid cipherId, AccessPreCheckResult result)
        : base("accessPreCheck")
    {
        CipherId = cipherId;
        ApprovalMode = result.ApprovalMode == AccessApprovalMode.Human ? "human" : "automatic";
        HasActiveLease = result.HasActiveLease;
    }

    public Guid CipherId { get; }

    /// <summary>
    /// <c>"automatic"</c> when a request would be approved immediately, <c>"human"</c> when it needs an approver.
    /// </summary>
    public string ApprovalMode { get; }

    /// <summary>
    /// True when the caller already holds an active lease: reveal the credential, no request needed.
    /// </summary>
    public bool HasActiveLease { get; }
}
