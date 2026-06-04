using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The result of submitting an access request. On the <see cref="AccessApprovalOutcome.Automatic"/> path a
/// <see cref="Lease"/> is issued immediately; on the <see cref="AccessApprovalOutcome.Human"/> path a pending
/// <see cref="LeaseRequest"/> is created to await an approver.
/// </summary>
public sealed record AccessRequestResult(
    AccessApprovalOutcome Outcome,
    Lease? Lease = null,
    LeaseRequest? Request = null)
{
    public static AccessRequestResult Automatic(Lease lease) =>
        new(AccessApprovalOutcome.Automatic, Lease: lease);

    public static AccessRequestResult Human(LeaseRequest request) =>
        new(AccessApprovalOutcome.Human, Request: request);
}
