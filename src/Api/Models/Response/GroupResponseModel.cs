using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Response;

public class GroupResponseModel : ResponseModel
{
    public GroupResponseModel(Group group, string obj = "group")
        : base(obj)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        Id = group.Id;
        OrganizationId = group.OrganizationId;
        Name = group.Name;
        AccessAll = group.AccessAll;
        ExternalId = group.ExternalId;
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; }
    public bool AccessAll { get; set; }
    public string ExternalId { get; set; }
}

public class GroupDetailsResponseModel : GroupResponseModel
{
    public GroupDetailsResponseModel(Group group, IEnumerable<CollectionAccessSelection> collections)
        : base(group, "groupDetails")
    {
        Collections = collections.Select(c => new SelectionReadOnlyResponseModel(c));
    }

    public IEnumerable<SelectionReadOnlyResponseModel> Collections { get; set; }
}
