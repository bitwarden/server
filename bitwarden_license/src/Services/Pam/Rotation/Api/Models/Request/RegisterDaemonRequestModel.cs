using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// Registers a new rotation daemon (spec <c>DaemonRegistration</c>). <see cref="EncryptedPayload"/> and
/// <see cref="Key"/> are the client-wrapped org key -- the admin's client wraps the org key and uploads only
/// ciphertext, exactly as Secrets Manager's <c>AccessTokenCreateRequestModel</c> does for a service account token;
/// the server never sees the plaintext key (zero-knowledge). Unlike that model's <c>Name</c>, this daemon's
/// <see cref="Name"/> is a plaintext display label (mirrors <c>Bit.Pam.Entities.PamDaemon.Name</c>), not an
/// encrypted field.
/// </summary>
public class RegisterDaemonRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [EncryptedString]
    [EncryptedStringLength(4000)]
    public string EncryptedPayload { get; set; } = null!;

    [Required]
    [EncryptedString]
    public string Key { get; set; } = null!;
}
