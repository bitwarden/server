using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Account recovery administration policy.
/// </summary>
public class ResetPasswordPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// List of Organization Ids that require automatic enrollment in password recovery.
    /// </summary>
    public IEnumerable<Guid> AutoEnroll { get; init; }

    /// <summary>
    /// Returns true if provided organizationId requires automatic enrollment in password recovery.
    /// </summary>
    public bool AutoEnrollEnabled(Guid organizationId)
    {
        return AutoEnroll.Contains(organizationId);
    }
}

public class ResetPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<ResetPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.ResetPassword;

    protected override bool ExemptProviders => false;

    protected override IEnumerable<OrganizationUserType> ExemptRoles => [];

    public override ResetPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = policyDetails
            .Aggregate(
                new ResetPasswordPolicyRequirement() { AutoEnroll = [] },
                (result, data) =>
                {
                    var dataModel = data.GetDataModel<ResetPasswordDataModel>();
                    if (dataModel.AutoEnrollEnabled && !result.AutoEnroll.Contains(data.OrganizationId))
                    {
                        return new ResetPasswordPolicyRequirement() { AutoEnroll = result.AutoEnroll.Append(data.OrganizationId) };
                    }

                    return result;
                });

        return result;
    }
}
