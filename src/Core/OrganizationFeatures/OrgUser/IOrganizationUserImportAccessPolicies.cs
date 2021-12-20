using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public interface IOrganizationUserImportAccessPolicies
    {
        AccessPolicyResult CanImport(Organization organization);
        AccessPolicyResult CanUseGroups(Organization organization);
    }
}
