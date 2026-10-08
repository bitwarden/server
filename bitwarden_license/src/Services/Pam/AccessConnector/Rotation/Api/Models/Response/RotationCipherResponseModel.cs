using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// The cipher for an access connector's claimed, executing attempt. Not the general <c>CipherResponseModel</c>, which
/// is bound to a user principal; <see cref="Data"/> is returned as stored and never decrypted.
/// </summary>
public class RotationCipherResponseModel
{
    public RotationCipherResponseModel(Cipher cipher)
    {
        ArgumentNullException.ThrowIfNull(cipher);

        CipherId = cipher.Id;
        OrganizationId = cipher.OrganizationId!.Value;
        Type = cipher.Type;
        Data = cipher.Data;
        Key = cipher.Key;
        RevisionDate = cipher.RevisionDate.AsUtc();
    }

    public Guid CipherId { get; set; }

    public Guid OrganizationId { get; set; }

    public CipherType Type { get; set; }

    /// <summary>The cipher's encrypted JSON, as stored.</summary>
    public string Data { get; set; } = null!;

    /// <summary>
    /// The cipher's own wrapped key. Null when the cipher is encrypted under the organization key directly.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>Sent back as the last-known revision when writing the rotated secret.</summary>
    public DateTime RevisionDate { get; set; }
}
