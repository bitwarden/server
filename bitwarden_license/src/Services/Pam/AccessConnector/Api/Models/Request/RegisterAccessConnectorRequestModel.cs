using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Request;

/// <summary>
/// Registers a new access connector (spec <c>ConnectorRegistration</c>). The organization key arrives wrapped
/// client-side in <see cref="EncryptedPayload"/> and <see cref="Key"/>, so the server never sees it in plaintext.
/// </summary>
public class RegisterAccessConnectorRequestModel
{
    /// <summary>The access connector's display label, sent in plaintext.</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The organization key, wrapped client-side with the encryption-key half of the access connector's credential.
    /// Returned on every token response so the access connector can unwrap it locally.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(4000)]
    public string EncryptedPayload { get; set; } = null!;

    /// <summary>The key protecting <see cref="EncryptedPayload"/>, wrapped with the organization key.</summary>
    [Required]
    [EncryptedString]
    public string Key { get; set; } = null!;
}
