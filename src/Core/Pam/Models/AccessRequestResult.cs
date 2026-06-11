using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The result of submitting an access request. Neither path mints a lease at submit: the
/// <see cref="AccessApprovalMode.Automatic"/> path creates an already-<see cref="AccessRequestStatus.Approved"/>
/// <see cref="AccessRequest"/> the requester then activates to start the lease, while the
/// <see cref="AccessApprovalMode.Human"/> path creates a <see cref="AccessRequestStatus.Pending"/> request to await
/// an approver. <see cref="ApprovalMode"/> tells the client which workflow to present.
/// </summary>
public sealed record AccessRequestResult(
    AccessApprovalMode ApprovalMode,
    AccessRequest Request)
{
    public static AccessRequestResult Automatic(AccessRequest request) =>
        new(AccessApprovalMode.Automatic, request);

    public static AccessRequestResult Human(AccessRequest request) =>
        new(AccessApprovalMode.Human, request);
}
