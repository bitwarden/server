#nullable enable

using System.Text.Json;
using Bit.Core.Tools.Enums;

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// A text secret being sent.
/// </summary>
public class SendItemData : SendData
{
    /// <summary>
    /// Instantiates a <see cref="SendItemData"/>.
    /// </summary>
    public SendItemData() { }

    /// <inheritdoc cref="SendItemData()"/>
    /// <param name="name">The name of the Send</param>
    /// <param name="notes">User-provided private notes of the send.</param>
    /// <param name="encryptionVersion">The version of Send encryption being used</param>
    /// <param name="data">Encrypted Send data</param>
    /// <param name="metadata">Unencrypted Send item metadata</param>
    public SendItemData(string name, string? notes, SendEncryptionType encryptionVersion, string? data,
        SendItemMetadata metadata)
        : base(name, notes)
    {
        EncryptionVersion = encryptionVersion;
        Data = data;
        Metadata = metadata;
    }

    public SendEncryptionType EncryptionVersion { get; set; } = SendEncryptionType.V1;

    public string? Data { get; set; }

    public SendItemMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Reads the vault item id from an item-type Send's data.
    /// </summary>
    /// <param name="data">The JSON stored in the Send's Data column.</param>
    /// <returns>
    /// The item id, or <see langword="null"/> when the data is missing, malformed, or has no item id.
    /// </returns>
    public static Guid? TryGetItemId(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        try
        {
            var itemId = JsonSerializer.Deserialize<SendItemData>(data)?.Metadata?.ItemId ?? Guid.Empty;
            return itemId == Guid.Empty ? null : itemId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
