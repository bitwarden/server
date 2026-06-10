using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The result of submitting an access request. On the <see cref="AccessApprovalMode.Automatic"/> path an
/// <see cref="AccessLease"/> is issued immediately; on the <see cref="AccessApprovalMode.Human"/> path a pending
/// <see cref="AccessRequest"/> is created to await an approver.
/// </summary>
public sealed record AccessRequestResult(
    AccessApprovalMode ApprovalMode,
    AccessLease? Lease = null,
    AccessRequest? Request = null)
{
    public static AccessRequestResult Automatic(AccessLease lease) =>
        new(AccessApprovalMode.Automatic, Lease: lease);

    public static AccessRequestResult Human(AccessRequest request) =>
        new(AccessApprovalMode.Human, Request: request);
}
