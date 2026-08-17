namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// The lifecycle state of an access request, as it appears on the wire: <c>0 = pending</c>, <c>1 = approved</c>,
/// <c>2 = activated</c>, <c>3 = denied</c>, <c>4 = canceled</c>, <c>5 = expired</c>.
/// </summary>
/// <remarks>
/// A standalone copy of the domain request status so the DTOs carry the wire contract without coupling to the PAM
/// domain; <see cref="DomainEnumMapping"/> converts between the two (deriving <see cref="Activated"/>, which the
/// domain does not track as a distinct state).
/// </remarks>
public enum AccessRequestStatus : byte
{
    /// <summary>Opened and awaiting a decision.</summary>
    Pending = 0,

    /// <summary>Approved, but no lease has been activated yet.</summary>
    Approved = 1,

    /// <summary>Approved and activated into a lease.</summary>
    Activated = 2,

    /// <summary>Rejected by a decision; no lease is produced.</summary>
    Denied = 3,

    /// <summary>Withdrawn by the requester before it was decided.</summary>
    Canceled = 4,

    /// <summary>Approved but lapsed before it was activated.</summary>
    Expired = 5,
}
