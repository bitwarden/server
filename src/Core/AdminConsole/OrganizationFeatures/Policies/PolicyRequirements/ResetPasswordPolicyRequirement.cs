using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Account recovery administration policy.
/// </summary>
public class ResetPasswordPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates that new members will automatically be enrolled in account recovery administration when joining an organization.
    /// </summary>
    public bool AutoEnrollEnabled { get; init; }
}

public class ResetPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<ResetPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.ResetPassword;

    public override ResetPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = policyDetails
            .Select(p => p.GetDataModel<ResetPasswordDataModel>())
            .Aggregate(
                new ResetPasswordPolicyRequirement(),
                (result, data) => new ResetPasswordPolicyRequirement
                {
                    AutoEnrollEnabled = result.AutoEnrollEnabled || data.AutoEnrollEnabled
                });

        return result;
    }
}
