namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// The lifecycle state of an access lease, as it appears on the wire: <c>0 = active</c>, <c>1 = expired</c>,
/// <c>2 = revoked</c>, <c>3 = cancelled</c>.
/// </summary>
/// <remarks>
/// A standalone copy of the domain lease status so the DTOs carry the wire contract without coupling to the PAM
/// domain; <see cref="DomainEnumMapping"/> converts between the two.
/// </remarks>
public enum AccessLeaseStatus : byte
{
    /// <summary>The access window is open and the lease has not been revoked.</summary>
    Active = 0,

    /// <summary>The access window has closed on its own.</summary>
    Expired = 1,

    /// <summary>An operator ended the lease early, before its window closed.</summary>
    Revoked = 2,

    /// <summary>The holder ended their own lease early, as opposed to <see cref="Revoked"/> (an operator ended it).</summary>
    Cancelled = 3,
}
