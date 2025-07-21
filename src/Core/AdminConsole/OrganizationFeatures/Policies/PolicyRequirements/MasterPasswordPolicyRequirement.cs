using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Master Password Requirements policy.
/// </summary>
public class MasterPasswordPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether MasterPassword requirements are enabled for the user.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Master Password Policy data model associated with this Policy
    /// </summary>
    public MasterPasswordPolicyData? EnforcedOptions { get; init; }
}

public class MasterPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<MasterPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.MasterPassword;

    protected override bool ExemptProviders => false;

    protected override IEnumerable<OrganizationUserType> ExemptRoles => [];

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [];

    public override MasterPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = policyDetails
            .Select(p => p.GetDataModel<MasterPasswordPolicyData>())
            .Aggregate(
                new MasterPasswordPolicyRequirement(),
                (result, data) =>
                {
                    data.CombineWith(result.EnforcedOptions);
                    return new MasterPasswordPolicyRequirement
                    {
                        Enabled = true,
                        EnforcedOptions = data
                    };
                });

        return result;
    }
}
