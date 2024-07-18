using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Responses;

public class GroupDetailsQueryResponse
{
    public Group Group { get; set; }
    public IEnumerable<CollectionAccessSelection> CollectionAccessSelection { get; set; }
}
