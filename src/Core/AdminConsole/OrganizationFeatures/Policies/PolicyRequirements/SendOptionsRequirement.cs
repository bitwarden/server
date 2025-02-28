using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send and Send Options policies.
/// </summary>
public class SendOptionsRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether the user is prohibited from hiding their email from the recipient of a Send.
    /// </summary>
    public bool DisableHideEmail { get; init; }
}

public class SendPolicyRequirementFactory : SimpleRequirementFactory<SendOptionsRequirement>
{
    protected override IEnumerable<OrganizationUserType> ExemptRoles =>
        [OrganizationUserType.Owner, OrganizationUserType.Admin];

    public override PolicyType PolicyType => PolicyType.SendOptions;

    public override SendOptionsRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = policyDetails
            .Select(p => p.GetDataModel<SendOptionsPolicyData>())
            .Aggregate(
                new SendOptionsRequirement(),
                (result, data) => new SendOptionsRequirement
                {
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail
                });

        return result;
    }
}
