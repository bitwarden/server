using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IOrganizationUserUserDetailsQuery
{
    Task<IEnumerable<OrganizationUserUserDetails>> GetOrganizationUserUserDetails(
        OrganizationUserUserDetailsQueryRequest request
    );
}
