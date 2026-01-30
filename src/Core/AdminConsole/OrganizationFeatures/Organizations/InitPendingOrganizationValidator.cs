using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Error = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public interface IInitPendingOrganizationValidator
{
    /// <summary>
    /// Validates the invite token for an organization user.
    /// </summary>
    bool ValidateInviteToken(OrganizationUser orgUser, User user, string emailToken);

    /// <summary>
    /// Validates that the user's email matches the organization user's email.
    /// </summary>
    Error? ValidateUserEmail(OrganizationUser orgUser, User user);

    /// <summary>
    /// Validates that the organization is in the correct state for initialization.
    /// </summary>
    Error? ValidateOrganizationState(Organization org);

    /// <summary>
    /// Validates that the organization user's organization ID matches the expected organization ID.
    /// </summary>
    Error? ValidateOrganizationMatch(OrganizationUser orgUser, Guid organizationId);

    /// <summary>
    /// Validates policy requirements for the user joining the organization.
    /// </summary>
    Task<Error?> ValidatePoliciesAsync(User user, Guid organizationId);

    /// <summary>
    /// Validates business rules for the user joining the organization (e.g., free org admin limits).
    /// </summary>
    Task<Error?> ValidateBusinessRulesAsync(User user, Organization org, OrganizationUser orgUser);
}

public class InitPendingOrganizationValidator : IInitPendingOrganizationValidator
{
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IFeatureService _featureService;
    private readonly IPolicyService _policyService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public InitPendingOrganizationValidator(
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IFeatureService featureService,
        IPolicyService policyService,
        IPolicyRequirementQuery policyRequirementQuery,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IOrganizationUserRepository organizationUserRepository)
    {
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _featureService = featureService;
        _policyService = policyService;
        _policyRequirementQuery = policyRequirementQuery;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _organizationUserRepository = organizationUserRepository;
    }

    public bool ValidateInviteToken(OrganizationUser orgUser, User user, string emailToken)
    {
        return OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, emailToken, orgUser);
    }

    public Error? ValidateUserEmail(OrganizationUser orgUser, User user)
    {
        if (string.IsNullOrWhiteSpace(orgUser.Email) ||
            !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            return new EmailMismatchError();
        }

        return null;
    }

    public Error? ValidateOrganizationState(Organization org)
    {
        if (org.Enabled)
        {
            return new OrganizationAlreadyEnabledError();
        }

        if (org.Status != OrganizationStatusType.Pending)
        {
            return new OrganizationNotPendingError();
        }

        if (!string.IsNullOrEmpty(org.PublicKey) || !string.IsNullOrEmpty(org.PrivateKey))
        {
            return new OrganizationHasKeysError();
        }

        return null;
    }

    public Error? ValidateOrganizationMatch(OrganizationUser orgUser, Guid organizationId)
    {
        if (orgUser.OrganizationId != organizationId)
        {
            return new OrganizationMismatchError();
        }

        return null;
    }

    public async Task<Error?> ValidatePoliciesAsync(User user, Guid organizationId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers))
        {
            var autoConfirmReq = await _policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
            if (autoConfirmReq.CannotCreateNewOrganization())
            {
                return new SingleOrgPolicyViolationError();
            }
        }

        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            return new SingleOrgPolicyViolationError();
        }

        var twoFactorReq = await _policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
        if (twoFactorReq.IsTwoFactorRequiredForOrganization(organizationId) &&
            !await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return new TwoFactorRequiredError();
        }

        return null;
    }

    public async Task<Error?> ValidateBusinessRulesAsync(User user, Organization org, OrganizationUser orgUser)
    {
        if (org.PlanType == PlanType.Free &&
            (orgUser.Type == OrganizationUserType.Owner || orgUser.Type == OrganizationUserType.Admin))
        {
            var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(user.Id);
            if (adminCount > 0)
            {
                return new FreeOrgAdminLimitError();
            }
        }

        return null;
    }
}
