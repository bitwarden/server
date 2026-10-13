#nullable enable

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// Unencrypted metadata of an Item Send.
/// </summary>
public class SendItemMetadata
{
    /// <summary>
    /// Id of the vault item (cipher) being sent.
    /// </summary>
    public Guid ItemId { get; set; }
}
