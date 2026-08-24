using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of submitting an access request. Neither path mints a lease at submit: the
/// <see cref="AccessApprovalMode.Automatic"/> path creates an already-<see cref="AccessRequestStatus.Approved"/>
/// <see cref="AccessRequest"/> the requester then activates to start the lease, while the
/// <see cref="AccessApprovalMode.Human"/> path creates a <see cref="AccessRequestStatus.Pending"/> request to await
/// an approver. <see cref="ApprovalMode"/> tells the client which workflow to present.
/// </summary>
/// <param name="ApprovalMode">Which workflow resolved the submission.</param>
/// <param name="Request">The request that was created.</param>
/// <param name="Decision">
/// The automatic verdict recorded alongside an auto-approved request, or null on the
/// <see cref="AccessApprovalMode.Human"/> path (which records no decision until an approver acts). Carried here
/// because the submission response reports the request's decision log, and this decision is written in the same
/// operation as the request rather than read back.
/// </param>
public sealed record AccessRequestResult(
    AccessApprovalMode ApprovalMode,
    AccessRequest Request,
    AccessDecision? Decision = null)
{
    public static AccessRequestResult Automatic(AccessRequest request, AccessDecision decision) =>
        new(AccessApprovalMode.Automatic, request, decision);

    public static AccessRequestResult Human(AccessRequest request) =>
        new(AccessApprovalMode.Human, request);
}
