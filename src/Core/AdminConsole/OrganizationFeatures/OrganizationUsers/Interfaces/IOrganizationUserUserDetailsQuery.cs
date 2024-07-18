using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Responses;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IOrganizationUserUserDetailsQuery
{
    Task<IEnumerable<OrganizationUserUserDetailsQueryResponse>> GetOrganizationUserUserDetails(OrganizationUserUserDetailsQueryRequest request);
}
