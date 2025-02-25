using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send and Send Options policies.
/// </summary>
public class DisableSendRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Send is disabled for the user. If true, the user should not be able to create or edit Sends.
    /// They may still delete existing Sends.
    /// </summary>
    public bool DisableSend { get; init; }
}

public class DisableSendRequirementFactory : SimpleRequirementFactory<DisableSendRequirement>
{
    protected override IEnumerable<OrganizationUserType> ExemptRoles =>
        [OrganizationUserType.Owner, OrganizationUserType.Admin];

    public override PolicyType PolicyType => PolicyType.DisableSend;

    public override DisableSendRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = new DisableSendRequirement { DisableSend = policyDetails.Any() };
        return result;
    }
}
