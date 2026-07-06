namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// The lifecycle state of an access lease, as it appears on the wire: <c>0 = active</c>, <c>1 = expired</c>,
/// <c>2 = revoked</c>.
/// </summary>
/// <remarks>
/// Scaffold only: a standalone copy of the domain lease status so the DTOs carry the wire contract without coupling to
/// the PAM domain. The real type lands with the rest of the PAM feature.
/// </remarks>
public enum AccessLeaseStatus : byte
{
    /// <summary>The access window is open and the lease has not been revoked.</summary>
    Active = 0,

    /// <summary>The access window has closed on its own.</summary>
    Expired = 1,

    /// <summary>The lease was ended early, before its window closed.</summary>
    Revoked = 2,
}
