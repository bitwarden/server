using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Mappings;

public static class OrganizationMappings
{
    public static OrgSignUpWithPlan WithPlan(this OrganizationSignup signup) =>
        new(signup, StaticStore.GetPlan(signup.Plan));
}
