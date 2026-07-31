using Bit.HttpExtensions;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// The resolved approval outcome for a cipher, without submitting a request — lets the client present the right
/// workflow (pick a duration vs. pick a window and justify) before the requester commits.
/// </summary>
public class AccessPreCheckResponseModel : ResponseModel
{
    public AccessPreCheckResponseModel()
        : base("accessPreCheck")
    {
    }

    public Guid CipherId { get; set; }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when a request would be approved immediately,
    /// <see cref="AccessApprovalMode.Human"/> when it needs an approver.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; set; }

    /// <summary>
    /// True when the caller already holds an active lease: reveal the credential, no request needed.
    /// </summary>
    public bool HasActiveLease { get; set; }
}
