using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IOrganizationUserUserDetailsQuery
{
    Task<IEnumerable<OrganizationUserUserDetails>> GetOrganizationUserUserDetails(OrganizationUserUserDetailsQueryRequest request);

    Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> Get(OrganizationUserUserDetailsQueryRequest request);

    Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> GetAccountRecoveryEnrolledUsers(OrganizationUserUserDetailsQueryRequest request);
}
