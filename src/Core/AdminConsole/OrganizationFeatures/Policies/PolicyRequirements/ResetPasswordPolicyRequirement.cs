using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Account recovery administration policy.
/// </summary>
public class ResetPasswordPolicyRequirement : IPolicyRequirement
{
    private readonly Dictionary<Guid, bool> _autoEnrollStatusByOrganizationId;

    public ResetPasswordPolicyRequirement(IEnumerable<PolicyDetails> policyDetails)
    {
        _autoEnrollStatusByOrganizationId = policyDetails.ToDictionary(
            k => k.OrganizationId,
            v => v.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled);
    }

    /// <summary>
    /// Returns true if provided organizationId requires automatic enrollment in password recovery.
    /// </summary>
    public bool AutoEnrollEnabled(Guid organizationId)
        => _autoEnrollStatusByOrganizationId.GetValueOrDefault(organizationId, false);

    /// <summary>
    /// Returns true if provided organizationId has the policy enabled.
    /// </summary>
    public bool IsEnabled(Guid organizationId)
        => _autoEnrollStatusByOrganizationId.ContainsKey(organizationId);
}

public class ResetPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<ResetPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.ResetPassword;

    protected override bool ExemptProviders => false;

    protected override IEnumerable<OrganizationUserType> ExemptRoles => [];

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [OrganizationUserStatusType.Revoked];

    public override ResetPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
        => new(policyDetails);
}
