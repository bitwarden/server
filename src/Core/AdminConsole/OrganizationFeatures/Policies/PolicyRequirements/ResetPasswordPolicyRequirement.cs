using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Account recovery administration policy.
/// </summary>
public class ResetPasswordPolicyRequirement : ISinglePolicyRequirement
{
    public bool AutoEnrollRequired { get; init; }
}

public class ResetPasswordPolicyRequirementFactory : ISinglePolicyRequirementFactory<ResetPasswordPolicyRequirement>
{
    public PolicyType PolicyType => PolicyType.ResetPassword;
    public bool ExemptRoles(OrganizationUserType role) => false;
    public bool ExemptProviders => false;
    public bool EnforceWhenAccepted => false;

    public ResetPasswordPolicyRequirement Create(PolicyDetails? policyDetails = null) => new()
    {
        AutoEnrollRequired = policyDetails != null && policyDetails.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled
    };
}
