using System.ComponentModel.DataAnnotations;
using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models;

public class SendItemMetadataModel
{
    public SendItemMetadataModel() { }

    public SendItemMetadataModel(SendItemMetadata metadata)
    {
        ItemId = metadata.ItemId;
    }

    [Required]
    public Guid ItemId { get; set; }

    public SendItemMetadata ToSendItemMetadata() => new() { ItemId = ItemId };
}
