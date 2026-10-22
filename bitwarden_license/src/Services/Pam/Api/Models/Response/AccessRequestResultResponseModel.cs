using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>The envelope returned when a cipher-lease request is submitted.</summary>
public class AccessRequestResultResponseModel : ResponseModel
{
    public AccessRequestResultResponseModel()
        : base("accessRequestResult")
    {
    }

    public AccessRequestResultResponseModel(AccessRequestResult result, DateTime now)
        : base("accessRequestResult")
    {
        ArgumentNullException.ThrowIfNull(result);

        ApprovalMode = result.ApprovalMode;
        Request = new AccessRequestDetailsResponseModel(ToDetails(result.Request, result.Decision, now));
    }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when <see cref="Request"/> was approved on submit,
    /// <see cref="AccessApprovalMode.Human"/> when it awaits an approver. Either way the requester activates it to
    /// start the lease.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; set; }

    /// <summary>
    /// The submitted request, without the requester's name and email or a produced lease.
    /// <see cref="AccessRequestDetailsResponseModel.Decisions"/> holds the automatic decision on the automatic path,
    /// and is empty otherwise.
    /// </summary>
    public AccessRequestDetailsResponseModel Request { get; set; } = null!;

    private static AccessRequestDetails ToDetails(AccessRequest request, AccessDecision? decision, DateTime now)
    {
        // Submit refuses end <= now, so this is Pending or Approved.
        var details = AccessRequestDetails.From(request, now);
        details.Decisions = decision is null
            ? []
            : [
                new AccessRequestDecision
                {
                    DeciderKind = decision.DeciderKind,
                    ApproverId = decision.ApproverId,
                    Comment = decision.Comment,
                    Verdict = decision.Verdict,
                    DecidedAt = decision.CreationDate,
                },
            ];
        return details;
    }
}
