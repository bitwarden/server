// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
    private IEnumerable<Guid> _autoEnrollOrganizations;
    public IEnumerable<Guid> AutoEnrollOrganizations { init => _autoEnrollOrganizations = value; }

    /// <summary>
    /// Returns true if provided organizationId requires automatic enrollment in password recovery.
    /// </summary>
    public bool AutoEnrollEnabled(Guid organizationId)
    {
        return _autoEnrollOrganizations.Contains(organizationId);
    }


}

public class ResetPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<ResetPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.ResetPassword;

    protected override bool ExemptProviders => false;

    protected override IEnumerable<OrganizationUserType> ExemptRoles => [];

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [OrganizationUserStatusType.Revoked];

    public override ResetPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = policyDetails
        .Where(p => p.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled)
        .Select(p => p.OrganizationId)
        .ToHashSet();

        return new ResetPasswordPolicyRequirement() { AutoEnrollOrganizations = result };
    }
}
