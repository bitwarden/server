using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;
using Error = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public interface IInitPendingOrganizationValidator
{
    /// <summary>
    /// Validates all preconditions for initializing a pending organization.
    /// </summary>
    Task<ValidationResult<InitPendingOrganizationValidationRequest>> ValidateAsync(
        InitPendingOrganizationValidationRequest request);
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

    public async Task<ValidationResult<InitPendingOrganizationValidationRequest>> ValidateAsync(
        InitPendingOrganizationValidationRequest request)
    {
        if (!ValidateInviteToken(request.OrganizationUser, request.User, request.EmailToken))
        {
            return Invalid(request, new InvalidTokenError());
        }

        var emailError = ValidateUserEmail(request.OrganizationUser, request.User);
        if (emailError != null)
        {
            return Invalid(request, emailError);
        }

        var matchError = ValidateOrganizationMatch(request.OrganizationUser, request.OrganizationId);
        if (matchError != null)
        {
            return Invalid(request, matchError);
        }

        var stateError = ValidateOrganizationState(request.Organization);
        if (stateError != null)
        {
            return Invalid(request, stateError);
        }

        var policyError = await ValidatePoliciesAsync(request.User, request.OrganizationId);
        if (policyError != null)
        {
            return Invalid(request, policyError);
        }

        var limitError = await ValidateFreeOrganizationLimitAsync(
            request.User, request.Organization, request.OrganizationUser);
        if (limitError != null)
        {
            return Invalid(request, limitError);
        }

        return Valid(request);
    }

    private bool ValidateInviteToken(OrganizationUser orgUser, User user, string emailToken)
    {
        return OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, emailToken, orgUser);
    }

    private static Error? ValidateUserEmail(OrganizationUser orgUser, User user)
    {
        if (string.IsNullOrWhiteSpace(orgUser.Email) ||
            !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            return new EmailMismatchError();
        }

        return null;
    }

    private static Error? ValidateOrganizationState(Organization org)
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

    private static Error? ValidateOrganizationMatch(OrganizationUser orgUser, Guid organizationId)
    {
        if (orgUser.OrganizationId != organizationId)
        {
            return new OrganizationMismatchError();
        }

        return null;
    }

    private async Task<Error?> ValidatePoliciesAsync(User user, Guid organizationId)
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

    private async Task<Error?> ValidateFreeOrganizationLimitAsync(User user, Organization org, OrganizationUser orgUser)
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
