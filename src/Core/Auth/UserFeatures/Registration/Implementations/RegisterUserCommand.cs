using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bit.Core.Auth.UserFeatures.Registration.Implementations;

public class RegisterUserCommand : IRegisterUserCommand
{
    private readonly ILogger<RegisterUserCommand> _logger;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IFeatureService _featureService;

    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _registrationEmailVerificationTokenDataFactory;
    private readonly IDataProtector _organizationServiceDataProtector;
    private readonly IDataProtector _providerServiceDataProtector;

    private readonly IUserService _userService;
    private readonly IMailService _mailService;

    private readonly IValidateRedemptionTokenCommand _validateRedemptionTokenCommand;

    private readonly IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> _emergencyAccessInviteTokenDataFactory;

    private readonly string _disabledUserRegistrationExceptionMsg = "Open registration has been disabled by the system administrator.";

    public RegisterUserCommand(
            ILogger<RegisterUserCommand> logger,
            IGlobalSettings globalSettings,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationRepository organizationRepository,
            IPolicyRepository policyRepository,
            IOrganizationDomainRepository organizationDomainRepository,
            IFeatureService featureService,
            IDataProtectionProvider dataProtectionProvider,
            IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
            IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> registrationEmailVerificationTokenDataFactory,
            IUserService userService,
            IMailService mailService,
            IValidateRedemptionTokenCommand validateRedemptionTokenCommand,
            IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> emergencyAccessInviteTokenDataFactory)
    {
        _logger = logger;
        _globalSettings = globalSettings;
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _policyRepository = policyRepository;
        _organizationDomainRepository = organizationDomainRepository;
        _featureService = featureService;

        _organizationServiceDataProtector = dataProtectionProvider.CreateProtector(
            "OrganizationServiceDataProtector");
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _registrationEmailVerificationTokenDataFactory = registrationEmailVerificationTokenDataFactory;

        _userService = userService;
        _mailService = mailService;

        _validateRedemptionTokenCommand = validateRedemptionTokenCommand;
        _emergencyAccessInviteTokenDataFactory = emergencyAccessInviteTokenDataFactory;

        _providerServiceDataProtector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        _featureService = featureService;
    }

    public async Task<IdentityResult> RegisterUser(User user)
    {
        await ValidateEmailDomainNotBlockedAsync(user.Email);

        var result = await _userService.CreateUserAsync(user);
        if (result == IdentityResult.Success)
        {
            await _mailService.SendWelcomeEmailAsync(user);
        }

        return result;
    }

    public async Task<IdentityResult> RegisterSSOAutoProvisionedUserAsync(User user, Organization organization)
    {
        var result = await _userService.CreateUserAsync(user);
        if (result == IdentityResult.Success)
        {
            await SendWelcomeEmailAsync(user, organization);
        }

        return result;
    }

    public async Task<IdentityResult> RegisterUserViaOrganizationInviteToken(User user, string masterPasswordHash,
        string orgInviteToken, Guid? orgUserId)
    {
        TryValidateOrgInviteToken(orgInviteToken, orgUserId, user);
        var orgUser = await SetUserEmail2FaIfOrgPolicyEnabledAsync(orgUserId, user);
        if (orgUser == null && orgUserId.HasValue)
        {
            throw new BadRequestException("Invalid organization user invitation.");
        }
        await ValidateEmailDomainNotBlockedAsync(user.Email, orgUser?.OrganizationId);

        user.ApiKey = CoreHelpers.SecureRandomString(30);

        if (!string.IsNullOrEmpty(orgInviteToken) && orgUserId.HasValue)
        {
            user.EmailVerified = true;
        }

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        var organization = await GetOrganizationUserOrganization(orgUserId ?? Guid.Empty, orgUser);
        if (result == IdentityResult.Success)
        {
            var sentWelcomeEmail = false;
            if (!string.IsNullOrEmpty(user.ReferenceData))
            {
                var referenceData = JsonConvert.DeserializeObject<Dictionary<string, object>>(user.ReferenceData) ?? [];
                if (referenceData.TryGetValue("initiationPath", out var value))
                {
                    var initiationPath = value.ToString() ?? string.Empty;
                    await SendAppropriateWelcomeEmailAsync(user, initiationPath, organization);
                    sentWelcomeEmail = true;
                    if (!string.IsNullOrEmpty(initiationPath))
                    {
                        return result;
                    }
                }
            }

            if (!sentWelcomeEmail)
            {
                await SendWelcomeEmailAsync(user, organization);
            }
        }

        return result;
    }

    /// <summary>
    /// This method attempts to validate the org invite token if provided. If the token is invalid an exception is thrown.
    /// If there is no exception it is assumed the token is valid or not provided and open registration is allowed.
    /// </summary>
    /// <param name="orgInviteToken">The organization invite token.</param>
    /// <param name="orgUserId">The organization user ID.</param>
    /// <param name="user">The user being registered.</param>
    /// <exception cref="BadRequestException">If validation fails then an exception is thrown.</exception>
    private void TryValidateOrgInviteToken(string orgInviteToken, Guid? orgUserId, User user)
    {
        var orgInviteTokenProvided = !string.IsNullOrWhiteSpace(orgInviteToken);

        if (orgInviteTokenProvided && orgUserId.HasValue)
        {
            // We have token data so validate it
            if (IsOrgInviteTokenValid(orgInviteToken, orgUserId.Value, user.Email))
            {
                return;
            }

            // Token data is invalid
            if (_globalSettings.DisableUserRegistration)
            {
                throw new BadRequestException(_disabledUserRegistrationExceptionMsg);
            }

            throw new BadRequestException("Organization invite token is invalid.");
        }

        // no token data or missing token data
        // Throw if open registration is disabled and there isn't an org invite token or an org user id
        // as you can't register without them.
        if (_globalSettings.DisableUserRegistration)
        {
            throw new BadRequestException(_disabledUserRegistrationExceptionMsg);
        }

        // Open registration is allowed
        // if we have an org invite token but no org user id, then throw an exception as we can't validate the token
        if (orgInviteTokenProvided && !orgUserId.HasValue)
        {
            throw new BadRequestException("Organization invite token cannot be validated without an organization user id.");
        }

        // if we have an org user id but no org invite token, then throw an exception as that isn't a supported flow
        if (orgUserId.HasValue && string.IsNullOrWhiteSpace(orgInviteToken))
        {
            throw new BadRequestException("Organization user id cannot be provided without an organization invite token.");
        }

        // If both orgInviteToken && orgUserId are missing, then proceed with open registration
    }

    /// <summary>
    /// Validates the org invite token using the new tokenable logic first, then falls back to the old token validation logic for backwards compatibility.
    /// Will set the out parameter organizationWelcomeEmailDetails if the new token is valid. If the token is invalid then no welcome email needs to be sent
    /// so the out parameter is set to null.
    /// </summary>
    /// <param name="orgInviteToken">Invite token</param>
    /// <param name="orgUserId">Inviting Organization UserId</param>
    /// <param name="userEmail">User email</param>
    /// <returns>true if the token is valid false otherwise</returns>
    private bool IsOrgInviteTokenValid(string orgInviteToken, Guid orgUserId, string userEmail)
    {
        // TODO: PM-4142 - remove old token validation logic once 3 releases of backwards compatibility are complete
        var newOrgInviteTokenValid = OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, orgInviteToken, orgUserId, userEmail);
        return newOrgInviteTokenValid || CoreHelpers.UserInviteTokenIsValid(
            _organizationServiceDataProtector, orgInviteToken, userEmail, orgUserId, _globalSettings);
    }


    /// <summary>
    /// Handles initializing the user with Email 2FA enabled if they are subject to an enabled 2FA organizational policy.
    /// </summary>
    /// <param name="orgUserId">The optional org user id</param>
    /// <param name="user">The newly created user object which could be modified</param>
    /// <returns>The organization user if one exists for the provided org user id, null otherwise</returns>
    private async Task<OrganizationUser?> SetUserEmail2FaIfOrgPolicyEnabledAsync(Guid? orgUserId, User user)
    {
        if (!orgUserId.HasValue)
        {
            return null;
        }

        var orgUser = await _organizationUserRepository.GetByIdAsync(orgUserId.Value);
        if (orgUser != null)
        {
            var twoFactorPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(orgUser.OrganizationId,
                PolicyType.TwoFactorAuthentication);
            if (twoFactorPolicy != null && twoFactorPolicy.Enabled)
            {
                user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
                {

                    [TwoFactorProviderType.Email] = new TwoFactorProvider
                    {
                        MetaData = new Dictionary<string, object> { ["Email"] = user.Email.ToLowerInvariant() },
                        Enabled = true
                    }
                });
                _userService.SetTwoFactorProvider(user, TwoFactorProviderType.Email);
            }
        }
        return orgUser;
    }


    private async Task SendAppropriateWelcomeEmailAsync(User user, string initiationPath, Organization? organization)
    {
        var isFromMarketingWebsite = initiationPath.Contains("Secrets Manager trial");

        if (isFromMarketingWebsite)
        {
            await _mailService.SendTrialInitiationEmailAsync(user.Email);
        }
        else
        {
            await SendWelcomeEmailAsync(user, organization);
        }
    }

    public async Task<IdentityResult> RegisterUserViaEmailVerificationToken(User user, string masterPasswordHash,
        string emailVerificationToken)
    {
        ValidateOpenRegistrationAllowed();
        await ValidateEmailDomainNotBlockedAsync(user.Email);

        var tokenable = ValidateRegistrationEmailVerificationTokenable(emailVerificationToken, user.Email);

        user.EmailVerified = true;
        user.Name = tokenable.Name;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await SendWelcomeEmailAsync(user);
        }

        return result;
    }

    public async Task<IdentityResult> RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(User user, string masterPasswordHash,
        string orgSponsoredFreeFamilyPlanInviteToken)
    {
        ValidateOpenRegistrationAllowed();
        await ValidateEmailDomainNotBlockedAsync(user.Email);
        await ValidateOrgSponsoredFreeFamilyPlanInviteToken(orgSponsoredFreeFamilyPlanInviteToken, user.Email);

        user.EmailVerified = true;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await SendWelcomeEmailAsync(user);
        }

        return result;
    }


    // TODO: in future, consider how we can consolidate base registration logic to reduce code duplication
    public async Task<IdentityResult> RegisterUserViaAcceptEmergencyAccessInviteToken(User user, string masterPasswordHash,
        string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        ValidateOpenRegistrationAllowed();
        await ValidateEmailDomainNotBlockedAsync(user.Email);
        ValidateAcceptEmergencyAccessInviteToken(acceptEmergencyAccessInviteToken, acceptEmergencyAccessId, user.Email);

        user.EmailVerified = true;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await SendWelcomeEmailAsync(user);
        }

        return result;
    }

    public async Task<IdentityResult> RegisterUserViaProviderInviteToken(User user, string masterPasswordHash,
        string providerInviteToken, Guid providerUserId)
    {
        ValidateOpenRegistrationAllowed();
        await ValidateEmailDomainNotBlockedAsync(user.Email);
        ValidateProviderInviteToken(providerInviteToken, providerUserId, user.Email);

        user.EmailVerified = true;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await SendWelcomeEmailAsync(user);
        }

        return result;
    }

    private void ValidateOpenRegistrationAllowed()
    {
        // We validate open registration on send of initial email and here b/c a user could technically start the
        // account creation process while open registration is enabled and then finish it after it has been
        // disabled by the self hosted admin.
        if (_globalSettings.DisableUserRegistration)
        {
            throw new BadRequestException(_disabledUserRegistrationExceptionMsg);
        }
    }

    private async Task ValidateOrgSponsoredFreeFamilyPlanInviteToken(string orgSponsoredFreeFamilyPlanInviteToken, string userEmail)
    {
        var (valid, sponsorship) = await _validateRedemptionTokenCommand.ValidateRedemptionTokenAsync(orgSponsoredFreeFamilyPlanInviteToken, userEmail);

        if (!valid)
        {
            throw new BadRequestException("Invalid org sponsored free family plan token.");
        }
    }

    private void ValidateAcceptEmergencyAccessInviteToken(string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId, string userEmail)
    {
        _emergencyAccessInviteTokenDataFactory.TryUnprotect(acceptEmergencyAccessInviteToken, out var tokenable);
        if (tokenable == null || !tokenable.Valid || !tokenable.IsValid(acceptEmergencyAccessId, userEmail))
        {
            throw new BadRequestException("Invalid accept emergency access invite token.");
        }
    }

    private void ValidateProviderInviteToken(string providerInviteToken, Guid providerUserId, string userEmail)
    {
        if (!CoreHelpers.TokenIsValid("ProviderUserInvite", _providerServiceDataProtector, providerInviteToken, userEmail, providerUserId,
                _globalSettings.OrganizationInviteExpirationHours))
        {
            throw new BadRequestException("Invalid provider invite token.");
        }
    }


    private RegistrationEmailVerificationTokenable ValidateRegistrationEmailVerificationTokenable(string emailVerificationToken, string userEmail)
    {
        _registrationEmailVerificationTokenDataFactory.TryUnprotect(emailVerificationToken, out var tokenable);
        if (tokenable == null || !tokenable.Valid || !tokenable.TokenIsValid(userEmail))
        {
            throw new BadRequestException("Invalid email verification token.");
        }

        return tokenable;
    }

    private async Task ValidateEmailDomainNotBlockedAsync(string email, Guid? excludeOrganizationId = null)
    {
        // Only check if feature flag is enabled
        if (!_featureService.IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation))
        {
            return;
        }

        var emailDomain = EmailValidation.GetDomain(email);

        var isDomainBlocked = await _organizationDomainRepository.HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(
            emailDomain, excludeOrganizationId);
        if (isDomainBlocked)
        {
            _logger.LogInformation(
                "User registration blocked by domain claim policy. Domain: {Domain}, ExcludedOrgId: {ExcludedOrgId}",
                emailDomain,
                excludeOrganizationId);
            throw new BadRequestException("This email address is claimed by an organization using Bitwarden.");
        }
    }

    /// <summary>
    /// We send different welcome emails depending on whether the user is joining a free/family or an enterprise organization. If information to populate the
    /// email isn't present we send the standard individual welcome email.
    /// </summary>
    /// <param name="user">Target user for the email</param>
    /// <param name="organization">this value is nullable</param>
    /// <returns></returns>
    private async Task SendWelcomeEmailAsync(User user, Organization? organization = null)
    {
        // Check if feature is enabled
        // TODO: Remove Feature flag: PM-28221
        if (!_featureService.IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates))
        {
            await _mailService.SendWelcomeEmailAsync(user);
            return;
        }

        // Most emails are probably for non organization users so we default to that experience
        if (organization == null)
        {
            await _mailService.SendIndividualUserWelcomeEmailAsync(user);
        }
        // We need to make sure that the organization email has the correct data to display otherwise we just send the standard welcome email
        else if (!string.IsNullOrEmpty(organization.DisplayName()))
        {
            // If the organization is Free or Families plan, send families welcome email
            if (organization.PlanType is PlanType.FamiliesAnnually
                or PlanType.FamiliesAnnually2019
                or PlanType.Free)
            {
                await _mailService.SendFreeOrgOrFamilyOrgUserWelcomeEmailAsync(user, organization.DisplayName());
            }
            else
            {
                await _mailService.SendOrganizationUserWelcomeEmailAsync(user, organization.DisplayName());
            }
        }
        // If the organization data isn't present send the standard welcome email
        else
        {
            await _mailService.SendIndividualUserWelcomeEmailAsync(user);
        }
    }

    private async Task<Organization?> GetOrganizationUserOrganization(Guid orgUserId, OrganizationUser? orgUser = null)
    {
        var organizationUser = orgUser ?? await _organizationUserRepository.GetByIdAsync(orgUserId);
        if (organizationUser == null)
        {
            return null;
        }

        return await _organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
    }
}
