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
    /// <summary>
    /// Indicates whether the user can use passkey login.
    /// </summary>
    /// <remarks>
    /// The user can use passkey login if they are not a member (Accepted/Confirmed) of an organization
    /// that has the Require SSO policy enabled.
    /// </remarks>
    public bool CanUsePasskeyLogin { get; init; }

    /// <summary>
    /// Indicates whether SSO requirement is enforced for the user.
    /// </summary>
    /// <remarks>
    /// The user is required to login with SSO if they are a confirmed member of an organization
    /// that has the Require SSO policy enabled.
    /// </remarks>
    public bool SsoRequired { get; init; }
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

    public override RequireSsoPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = new RequireSsoPolicyRequirement
        {
            CanUsePasskeyLogin = policyDetails.All(p =>
                p.OrganizationUserStatus == OrganizationUserStatusType.Revoked ||
                p.OrganizationUserStatus == OrganizationUserStatusType.Invited),

            SsoRequired = policyDetails.Any(p =>
                p.OrganizationUserStatus == OrganizationUserStatusType.Confirmed)
        };

        return result;
    }
}
