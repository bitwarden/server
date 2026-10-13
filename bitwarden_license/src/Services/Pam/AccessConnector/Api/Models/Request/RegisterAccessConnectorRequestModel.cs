using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Request;

/// <summary>
/// Registers a new access connector (spec <c>ConnectorRegistration</c>). <see cref="EncryptedPayload"/> and
/// <see cref="Key"/> carry the organization key wrapped client-side, so the server holds ciphertext only and never
/// sees the plaintext key. <see cref="Name"/> is a plaintext display label.
/// </summary>
public class RegisterAccessConnectorRequestModel
{
    /// <summary>The access connector's plaintext display label, shown wherever access connectors are listed and
    /// managed.</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The organization key, wrapped client-side with the encryption-key half of the access connector's credential.
    /// Stored verbatim and handed back on every token response so the access connector can unwrap it locally; the
    /// server only ever holds the ciphertext.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(4000)]
    public string EncryptedPayload { get; set; } = null!;

    /// <summary>
    /// The key protecting <see cref="EncryptedPayload"/>, itself uploaded wrapped -- opaque ciphertext the server
    /// stores but can never use, recoverable only client-side.
    /// </summary>
    [Required]
    [EncryptedString]
    public string Key { get; set; } = null!;
}
