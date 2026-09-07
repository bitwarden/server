using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of submitting an access request. Neither path mints a lease at submit: the
/// <see cref="AccessApprovalMode.Automatic"/> path creates an already-<see cref="AccessRequestStatus.Approved"/>
/// request the requester then activates, while the <see cref="AccessApprovalMode.Human"/> path creates a
/// <see cref="AccessRequestStatus.Pending"/> request awaiting an approver.
/// </summary>
/// <param name="ApprovalMode">Which workflow resolved the submission.</param>
/// <param name="Request">The request that was created.</param>
/// <param name="Decision">
/// The automatic verdict recorded alongside an auto-approved request, or null on the
/// <see cref="AccessApprovalMode.Human"/> path. Carried here since it's written in the same operation as the
/// request rather than read back.
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
