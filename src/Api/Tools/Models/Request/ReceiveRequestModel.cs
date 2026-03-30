using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReceiveFeatures.Models;
using Bit.Core.Utilities;

namespace Bit.Api.Tools.Models.Request;

/// <summary>
/// A Receive request issued by a Bitwarden client
/// </summary>
public class ReceiveRequestModel
{
    /// <summary>
    /// Label for the Receive.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string Name { get; set; }

    /// <summary>
    /// The public key wrapped by the shared content encryption key (SCEK).
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string ScekWrappedPublicKey { get; set; }

    /// <summary>
    /// The shared content encryption key (SCEK) wrapped by the owners userKey.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string UserKeyWrappedSharedContentEncryptionKey { get; set; }

    /// <summary>
    /// The private key wrapped by the owners userKey.
    /// </summary>
    [Required]
    [EncryptedString]
    public required string UserKeyWrappedPrivateKey { get; set; }

    /// <summary>
    /// The date this Receive becomes unavailable to potential uploaders.
    /// </summary>
    public DateTime ExpirationDate { get; set; }

    public Receive ToReceive(Guid userId)
    {
        var receive = new Receive
        {
            UserId = userId,
            Name = Name,
            Data = JsonSerializer.Serialize(new ReceiveFileData(Name, string.Empty), JsonHelpers.IgnoreWritingNull),
            UserKeyWrappedSharedContentEncryptionKey = UserKeyWrappedSharedContentEncryptionKey,
            UserKeyWrappedPrivateKey = UserKeyWrappedPrivateKey,
            ScekWrappedPublicKey = ScekWrappedPublicKey,
            Secret = CoreHelpers.SecureRandomString(42),
            ExpirationDate = ExpirationDate
        };

        return receive;
    }


    public ReceiveUpdateData ToUpdateData(Guid id)
    {
        return new ReceiveUpdateData { Id = id, Name = Name, ExpirationDate = ExpirationDate, };
    }


}

