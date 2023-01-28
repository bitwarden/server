using Bit.Core.Entities;

namespace Bit.Api.Models.Public.Request;

public class CollectionUpdateRequestModel : CollectionBaseModel
{
    /// <summary>
    /// The associated groups that this collection is assigned to.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsRequestModel> Groups { get; set; }

    public Collection ToCollection(Collection existingCollection)
    {
        existingCollection.ExternalId = ExternalId;
        return existingCollection;
    }
}
