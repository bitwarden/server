#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request;

public class AgentConnectionRequestModel
{
    /// <summary>The registered partner's OAuth client id (e.g. <c>partner.acme</c>).</summary>
    [Required]
    [StringLength(200)]
    public required string PartnerClientId { get; set; }

    [Required]
    [StringLength(200)]
    public required string Name { get; set; }

    /// <summary>
    /// The agent-access PSK encrypted with the user's key. Server-opaque ciphertext — the plaintext PSK
    /// never reaches the server.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public required string Key { get; set; }
}
