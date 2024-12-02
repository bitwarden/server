using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class RequireSsoPolicyRequirementFactory(IGlobalSettings globalSettings)
    : IPolicyRequirementFactory<RequireSsoPolicyRequirement>
{
    public PolicyType Type => PolicyType.RequireSso;

    public RequireSsoPolicyRequirement CreateRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Any());

    public bool EnforcePolicy(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus == OrganizationUserStatusType.Confirmed &&
        (globalSettings.Sso.EnforceSsoPolicyForAllUsers || !userPolicyDetails.IsAdminType());
}

public record RequireSsoPolicyRequirement(bool RequireSso) : IPolicyRequirement;
