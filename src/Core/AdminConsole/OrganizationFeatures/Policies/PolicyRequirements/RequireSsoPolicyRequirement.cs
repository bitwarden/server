using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Core.Settings;

/// <summary>
/// Policy requirements for the Require SSO policy.
/// </summary>
public class RequireSsoPolicyRequirement : IPolicyRequirement
{
    private readonly IEnumerable<PolicyDetails> _policyDetails;

    public RequireSsoPolicyRequirement(IEnumerable<PolicyDetails> policyDetails)
    {
        _policyDetails = policyDetails;
    }

    /// <summary>
    /// Indicates whether the user can use passkey login.
    /// Policy is enforced for users with status >= Accepted.
    /// </summary>
    public bool CanUsePasskeyLogin => _policyDetails.Any(p =>
        p.OrganizationUserStatus >= OrganizationUserStatusType.Accepted);

    /// <summary>
    /// Indicates whether SSO is required for login.
    /// Policy is enforced for users with status >= Confirmed.
    /// </summary>
    public bool SsoRequired => _policyDetails.Any(p =>
        p.OrganizationUserStatus >= OrganizationUserStatusType.Confirmed);
}


public class RequireSsoPolicyRequirementFactory : BasePolicyRequirementFactory<RequireSsoPolicyRequirement>
{
    private readonly GlobalSettings _globalSettings;

    public RequireSsoPolicyRequirementFactory(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public override PolicyType PolicyType => PolicyType.RequireSso;

    protected override IEnumerable<OrganizationUserType> ExemptRoles =>
        _globalSettings.Sso.EnforceSsoPolicyForAllUsers
            ? Array.Empty<OrganizationUserType>()
            : [OrganizationUserType.Owner, OrganizationUserType.Admin];

    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses =>
        Array.Empty<OrganizationUserStatusType>();

    public override RequireSsoPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = new RequireSsoPolicyRequirement(policyDetails);

        return result;
    }
}
