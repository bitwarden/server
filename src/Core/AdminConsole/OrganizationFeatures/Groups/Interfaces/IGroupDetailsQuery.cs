using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Responses;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;

public interface IGroupDetailsQuery
{
    Task<IEnumerable<GroupDetailsQueryResponse>> GetGroupDetails(GroupDetailsQueryRequest request);
}
