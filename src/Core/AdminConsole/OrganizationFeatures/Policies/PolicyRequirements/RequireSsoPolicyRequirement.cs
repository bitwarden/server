using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class RequireSsoPolicyRequirementDefinition : IPolicyRequirementDefinition<RequireSsoPolicyRequirement>
{
    private readonly IGlobalSettings _globalSettings;

    public RequireSsoPolicyRequirementDefinition(IGlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public PolicyType Type => PolicyType.RequireSso;

    public RequireSsoPolicyRequirement Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Any());

    public bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus == OrganizationUserStatusType.Confirmed &&
        (_globalSettings.Sso.EnforceSsoPolicyForAllUsers || !userPolicyDetails.IsAdminType());
}

public record RequireSsoPolicyRequirement(bool RequireSso) : IPolicyRequirement;
