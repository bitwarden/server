using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public class OrganizationUserImportAccessPolicies : BaseAccessPolicies, IOrganizationUserImportAccessPolicies
    {
        public AccessPolicyResult CanImport(Organization organization)
        {
            if (organization == null)
            {
                return Fail();
            }

            if (!organization.UseDirectory)
            {
                return Fail("Organization cannot use directory syncing.");
            }

            return Success;
        }

        public AccessPolicyResult CanUseGroups(Organization organization)
        {
            if (!organization.UseGroups)
            {
                return Fail("Organization cannot use groups.");
            }
            return Success;
        }
    }
}
