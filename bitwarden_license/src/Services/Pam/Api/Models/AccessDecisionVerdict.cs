namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// An approver's verdict on a pending access request, as it appears on the wire: <c>0 = deny</c>, <c>1 = approve</c>.
/// </summary>
/// <remarks>
/// Scaffold only: a standalone copy of the domain verdict so the DTOs carry the wire contract without coupling to the
/// PAM domain. The real type lands with the rest of the PAM feature.
/// </remarks>
public enum AccessDecisionVerdict : byte
{
    /// <summary>The request was rejected; no lease is produced.</summary>
    Deny = 0,

    /// <summary>The request was granted; an approved request can then be activated into a lease.</summary>
    Approve = 1,
}
