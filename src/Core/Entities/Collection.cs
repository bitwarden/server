using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class Collection : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = null!;
    [MaxLength(300)]
    public string? ExternalId { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public CollectionType Type { get; set; } = CollectionType.SharedCollection;
    public string? DefaultUserCollectionEmail { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
