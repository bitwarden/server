using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

/// <summary>
/// Server-side state backing a single remembered device's two-factor "remember me" token.
/// One row per (<see cref="UserId"/>, <see cref="DeviceId"/>).
/// </summary>
/// <remarks>
/// A token issued to a device carries a copy of that device's <see cref="Stamp"/>, and is honored only
/// while the two match. Writing a new <see cref="Stamp"/> therefore invalidates every token previously
/// issued for the device, leaving the user's other devices and their access and refresh tokens untouched.
/// </remarks>
public class TwoFactorRememberToken : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The owning user. Derivable through <see cref="DeviceId"/>, but stored directly so that
    /// revoking every remembered device for one user is an index seek.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The remembered device. Note this is <c>Device.Id</c>, not the client-supplied device identifier.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// The value a presented token's stamp is compared against. Replaced to revoke.
    /// </summary>
    [MaxLength(50)]
    public string Stamp { get; set; } = null!;

    /// <summary>
    /// When this device was first remembered. Preserved when the row is re-issued.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When <see cref="Stamp"/> was last written, whether by re-issuance or by a revocation.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this row stops being honored, regardless of <see cref="Stamp"/>.
    /// </summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
