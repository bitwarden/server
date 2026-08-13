using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;
using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Models.Response;

public class AccessPreCheckResponseModel : ResponseModel
{
    public AccessPreCheckResponseModel(Guid cipherId, AccessPreCheckResult result)
        : base("accessPreCheck")
    {
        CipherId = cipherId;
        ApprovalMode = result.ApprovalMode;
        HasActiveLease = result.HasActiveLease;
    }

    public Guid CipherId { get; }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when a request would be approved immediately,
    /// <see cref="AccessApprovalMode.Human"/> when it needs an approver.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; }

    /// <summary>
    /// True when the caller already holds an active lease: reveal the credential, no request needed.
    /// </summary>
    public bool HasActiveLease { get; }
}
