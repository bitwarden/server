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
    public Guid? Id { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Users { get; set; }

    public Collection ToCollection(Guid orgId)
    {
        return ToCollection(new Collection
        {
            OrganizationId = orgId
        });
    }

    public Collection ToCollection(Collection existingCollection)
    {
        existingCollection.Name = Name;
        existingCollection.ExternalId = ExternalId;
        if (Id != null && Id != Guid.Empty)
        {
            existingCollection.Id = Id ?? Guid.Empty;
        }
        return existingCollection;
    }
}

public class CollectionBulkDeleteRequestModel
{
    [Required]
    public IEnumerable<string> Ids { get; set; }
    public string OrganizationId { get; set; }
}
