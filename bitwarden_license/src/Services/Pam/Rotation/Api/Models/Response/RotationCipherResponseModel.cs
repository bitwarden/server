using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// The response to <c>GET rotation/attempts/{id}/cipher</c> -- purpose-built for the daemon's narrow read (only
/// this daemon's claimed, executing attempt; see <c>GetRotationCipherQuery</c>), deliberately not the general
/// <c>CipherResponseModel</c> (which is user-principal-bound). <see cref="Data"/> is the cipher's encrypted JSON
/// blob exactly as stored -- opaque ciphertext the server never decrypts.
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

    public Guid CipherId { get; }
    public Guid OrganizationId { get; }
    public CipherType Type { get; }

    /// <summary>The cipher's encrypted JSON blob, verbatim -- opaque ciphertext.</summary>
    public string Data { get; }

    public string? Key { get; }
    public DateTime RevisionDate { get; }
}
