using Bit.Pam.Entities;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of submitting an access request: approved for the requester to activate on the
/// <see cref="AccessApprovalMode.Automatic"/> path, pending an approver on the
/// <see cref="AccessApprovalMode.Human"/> path.
/// </summary>
/// <param name="ApprovalMode">Which workflow resolved the submission.</param>
/// <param name="Request">The request that was created.</param>
/// <param name="Decision">
/// The automatic verdict recorded with an auto-approved request; null on the human path.
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
