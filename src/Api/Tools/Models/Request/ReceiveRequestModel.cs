using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Tools.Models.Request;
using Bit.Core.Tools.Entities;
using Bit.Core.Utilities;

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
    [EncryptedStringLength(1000)]
    public required string UserKeyWrappedPrivateKey { get; set; }

    /// <summary>
    /// The date this Receive becomes unavailable to potential uploaders.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    public Receive ToReceive(Guid userId)
    {
        var receive = new Receive
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            Data = string.Empty, // TODO: how to construct this??  Points to storage locations for files associated with a receive and more.
            UserKeyWrappedSharedContentEncryptionKey = UserKeyWrappedSharedContentEncryptionKey,
            UserKeyWrappedPrivateKey = UserKeyWrappedPrivateKey,
            ScekWrappedPublicKey = ScekWrappedPublicKey,
            Secret = Guid.NewGuid().ToString(), // TODO: is this what we decided? It isn't sequential and so has greater entropy than comb.
            ExpirationDate = ExpirationDate
        };

        return receive;
    }
}

