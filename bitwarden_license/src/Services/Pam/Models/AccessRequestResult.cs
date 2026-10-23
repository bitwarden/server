using Bit.Pam.Entities;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Models;

/// <summary>
/// The result of submitting an access request: approved and awaiting the requester's activation on the
/// <see cref="AccessApprovalMode.Automatic"/> path, or pending an approver on the
/// <see cref="AccessApprovalMode.Human"/> path.
/// </summary>
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
