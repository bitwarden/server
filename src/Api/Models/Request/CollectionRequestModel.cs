using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request;

public class CollectionRequestModel
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Name { get; set; }

    [StringLength(300)]
    public string ExternalId { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Users { get; set; }

    public Collection ToCollection(Guid orgId)
    {
        return ToCollection(new Collection { OrganizationId = orgId });
    }

    public virtual Collection ToCollection(Collection existingCollection)
    {
        existingCollection.Name = Name;
        existingCollection.ExternalId = ExternalId;
        return existingCollection;
    }
}

public class CollectionBulkDeleteRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}

public class CollectionWithIdRequestModel : CollectionRequestModel
{
    public Guid? Id { get; set; }

    public override Collection ToCollection(Collection existingCollection)
    {
        existingCollection.Id = Id ?? Guid.Empty;
        return base.ToCollection(existingCollection);
    }
}
