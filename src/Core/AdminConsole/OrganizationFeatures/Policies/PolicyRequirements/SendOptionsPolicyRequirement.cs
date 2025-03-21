using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Send Options policy.
/// </summary>
public class SendOptionsPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether the user is prohibited from hiding their email from the recipient of a Send.
    /// </summary>
    public bool DisableHideEmail { get; init; }
}

public class SendOptionsPolicyRequirementFactory : BasePolicyRequirementFactory<SendOptionsPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SendOptions;

    public override SendOptionsPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = policyDetails
            .Select(p => p.GetDataModel<SendOptionsPolicyData>())
            .Aggregate(
                new SendOptionsPolicyRequirement(),
                (result, data) => new SendOptionsPolicyRequirement
                {
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail
                });

        return result;
    }
}
