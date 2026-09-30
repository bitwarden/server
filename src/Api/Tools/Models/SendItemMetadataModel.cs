using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models;

public class SendItemMetadataModel
{
    public SendItemMetadataModel() { }

    public SendItemMetadataModel(SendItemMetadata metadata)
    {
        ItemId = metadata.ItemId;
    }

    public Guid? ItemId { get; set; }

    public SendItemMetadata ToSendItemMetadata() => new() { ItemId = ItemId };
}
