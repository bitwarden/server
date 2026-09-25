using Bit.HttpExtensions;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// The envelope returned when a cipher-lease request is submitted.
/// </summary>
public class AccessRequestResultResponseModel : ResponseModel
{
    public AccessRequestResultResponseModel()
        : base("accessRequestResult")
    {
    }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when the <see cref="Request"/> was approved on submit and is ready
    /// to activate (the client shows "Start lease"), <see cref="AccessApprovalMode.Human"/> when it is pending an
    /// approver. No lease is minted at submit on either path; the requester activates the request to start the lease.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; set; }

    /// <summary>
    /// The submitted request. Fields that only a resolved or leased request carries are null here:
    /// <see cref="AccessRequestDetailsResponseModel.ProducedLeaseId"/> and
    /// <see cref="AccessRequestDetailsResponseModel.ProducedLeaseStatus"/> are always null at submit (no lease is
    /// minted on either path), and <see cref="AccessRequestDetailsResponseModel.Decisions"/> is empty unless
    /// <see cref="ApprovalMode"/> is <see cref="AccessApprovalMode.Automatic"/>, in which case it carries the
    /// single automatic decision.
    /// </summary>
    public AccessRequestDetailsResponseModel Request { get; set; } = null!;
}
