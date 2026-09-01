using  Bit.Core.Vault.Enums;
using Bit.Core.Vault.Entities;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// The response to <c>GET access-connectors/rotation/attempts/{id}/cipher</c> -- purpose-built for the access
/// connector's narrow read (only this access connector's claimed, executing attempt; see
/// <c>GetRotationCipherQuery</c>), deliberately not the general <c>CipherResponseModel</c> (which is
/// user-principal-bound). <see cref="Data"/> is the cipher's encrypted JSON blob exactly as stored -- opaque ciphertext
/// the server never decrypts.
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

    /// <summary>
    /// The cipher's unique identifier.
    /// </summary>
    public Guid CipherId { get; set; }

    /// <summary>
    /// The organization owning the cipher.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The cipher's type.
    /// </summary>
    public CipherType Type { get; set; }

    /// <summary>
    /// The cipher's encrypted JSON blob, verbatim -- opaque ciphertext.
    /// </summary>
    public string Data { get; set; } = null!;

    /// <summary>
    /// The cipher's own wrapped encryption key, when it has one -- opaque ciphertext. Null when the cipher is
    /// encrypted under the organization key directly.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// The cipher's current revision date (UTC) -- what the access connector sends back as its last-known revision when
    /// writing the rotated secret.
    /// </summary>
    public DateTime RevisionDate { get; set; }
}
