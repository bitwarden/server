using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Response;

public class CollectionResponseModel : ResponseModel
{
    public CollectionResponseModel(Collection collection, string obj = "collection")
        : base(obj)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        Id = collection.Id.ToString();
        OrganizationId = collection.OrganizationId.ToString();
        Name = collection.Name;
        ExternalId = collection.ExternalId;
    }

    public string Id { get; set; }
    public string OrganizationId { get; set; }
    public string Name { get; set; }
    public string ExternalId { get; set; }
}

public class CollectionDetailsResponseModel : CollectionResponseModel
{
    public CollectionDetailsResponseModel(CollectionDetails collectionDetails)
        : base(collectionDetails, "collectionDetails")
    {
        ReadOnly = collectionDetails.ReadOnly;
        HidePasswords = collectionDetails.HidePasswords;
    }

    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
}

public class CollectionGroupDetailsResponseModel : CollectionResponseModel
{
    public CollectionGroupDetailsResponseModel(Collection collection, IEnumerable<SelectionReadOnly> groups)
        : base(collection, "collectionGroupDetails")
    {
        Groups = groups.Select(g => new SelectionReadOnlyResponseModel(g));
    }

    public IEnumerable<SelectionReadOnlyResponseModel> Groups { get; set; }
}
