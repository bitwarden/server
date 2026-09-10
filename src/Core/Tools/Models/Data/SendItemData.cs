#nullable enable

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
    public SendItemData(string name, string? notes, SendEncryptionType encryptionVersion, string? data)
        : base(name, notes)
    {
        EncryptionVersion = encryptionVersion;
        Data = data;
    }

    public SendEncryptionType EncryptionVersion { get; set; } = SendEncryptionType.V1;

    public string? Data { get; set; }
}
