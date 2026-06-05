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
        Outcome = result.HasActiveLease
            ? "active"
            : result.Outcome == AccessApprovalOutcome.Human ? "human" : "automatic";
    }

    public Guid CipherId { get; }

    /// <summary>
    /// <c>"active"</c> when the caller already holds an active lease (reveal the credential, no request needed),
    /// <c>"automatic"</c> when a request would be approved immediately, <c>"human"</c> when it needs an approver.
    /// </summary>
    public string Outcome { get; }
}
