namespace Bit.Core.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.Lease"/>. Only <see cref="Active"/> leases authorize access.
/// </summary>
public enum LeaseStatus : byte
{
    Active = 0,
    Expired = 1,
    Revoked = 2,
}
