using System.ComponentModel.DataAnnotations;
using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models;

public class SendItemMetadataModel
{
    public SendItemMetadataModel() { }

    public SendItemMetadataModel(SendItemMetadata metadata)
    {
        ItemId = metadata.ItemId;
        FolderName = metadata.FolderName;
        CollectionNames = metadata.CollectionNames;
        OrganizationName = metadata.OrganizationName;
        CreationDate = metadata.CreationDate;
        RevisionDate = metadata.RevisionDate;
    }

    [Required]
    public Guid ItemId { get; set; }
    public string? FolderName { get; set; }
    public string[]? CollectionNames { get; set; }
    public string? OrganizationName { get; set; }
    [Required]
    public DateTime? CreationDate { get; set; }
    [Required]
    public DateTime? RevisionDate { get; set; }

    public SendItemMetadata ToSendItemMetadata()
    {
        if (!CreationDate.HasValue)
        {
            throw new ArgumentNullException(nameof(CreationDate), "CreationDate is required item metadata");
        }
        if (!RevisionDate.HasValue)
        {
            throw new ArgumentNullException(nameof(RevisionDate), "RevisionDate is required item metadata");
        }
        return new()
        {
            ItemId = ItemId,
            FolderName = FolderName,
            CollectionNames = CollectionNames,
            OrganizationName = OrganizationName,
            CreationDate = CreationDate!.Value,
            RevisionDate = RevisionDate!.Value
        };
    }
}
