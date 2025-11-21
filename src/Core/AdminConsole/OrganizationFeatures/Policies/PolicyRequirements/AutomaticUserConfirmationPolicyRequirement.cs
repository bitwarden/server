using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class AutomaticUserConfirmationPolicyRequirement(IEnumerable<PolicyDetails> policyDetails) : IPolicyRequirement
{
    public bool AutomaticUserConfirmationEnabledForOrganization => policyDetails.Any();
}

public class AutomaticUserConfirmationPolicyRequirementFactory : BasePolicyRequirementFactory<AutomaticUserConfirmationPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.AutomaticUserConfirmation;

    protected override IEnumerable<OrganizationUserType> ExemptRoles => [];

    public override AutomaticUserConfirmationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails) =>
        new(policyDetails);
}
