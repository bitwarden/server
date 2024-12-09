using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace Bit.Core.Auth.UserFeatures.Registration.Implementations;

public class RegisterUserCommand : IRegisterUserCommand
{

    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IReferenceEventService _referenceEventService;

    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _registrationEmailVerificationTokenDataFactory;
    private readonly IDataProtector _organizationServiceDataProtector;
    private readonly IDataProtector _providerServiceDataProtector;

    private readonly ICurrentContext _currentContext;

    private readonly IUserService _userService;
    private readonly IMailService _mailService;

    private readonly IValidateRedemptionTokenCommand _validateRedemptionTokenCommand;

    private readonly IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> _emergencyAccessInviteTokenDataFactory;

    private readonly string _disabledUserRegistrationExceptionMsg = "Open registration has been disabled by the system administrator.";

    public RegisterUserCommand(
        IGlobalSettings globalSettings,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        IReferenceEventService referenceEventService,
        IDataProtectionProvider dataProtectionProvider,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> registrationEmailVerificationTokenDataFactory,
        ICurrentContext currentContext,
        IUserService userService,
        IMailService mailService,
        IValidateRedemptionTokenCommand validateRedemptionTokenCommand,
        IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> emergencyAccessInviteTokenDataFactory
        )
    {
        _globalSettings = globalSettings;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _referenceEventService = referenceEventService;

        _organizationServiceDataProtector = dataProtectionProvider.CreateProtector(
            "OrganizationServiceDataProtector");
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _registrationEmailVerificationTokenDataFactory = registrationEmailVerificationTokenDataFactory;

        _currentContext = currentContext;
        _userService = userService;
        _mailService = mailService;

        _validateRedemptionTokenCommand = validateRedemptionTokenCommand;
        _emergencyAccessInviteTokenDataFactory = emergencyAccessInviteTokenDataFactory;

        _providerServiceDataProtector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
    }


    public async Task<IdentityResult> RegisterUser(User user)
    {
        var result = await _userService.CreateUserAsync(user);
        if (result == IdentityResult.Success)
        {
            await _mailService.SendWelcomeEmailAsync(user);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext));
        }

        return result;
    }

    public async Task<IdentityResult> RegisterUserViaOrganizationInviteToken(User user, string masterPasswordHash,
        string orgInviteToken, Guid? orgUserId)
    {
        ValidateOrgInviteToken(orgInviteToken, orgUserId, user);
        await SetUserEmail2FaIfOrgPolicyEnabledAsync(orgUserId, user);

        user.ApiKey = CoreHelpers.SecureRandomString(30);

        if (!string.IsNullOrEmpty(orgInviteToken) && orgUserId.HasValue)
        {
            user.EmailVerified = true;
        }

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            if (!string.IsNullOrEmpty(user.ReferenceData))
            {
                var referenceData = JsonConvert.DeserializeObject<Dictionary<string, object>>(user.ReferenceData);
                if (referenceData.TryGetValue("initiationPath", out var value))
                {
                    var initiationPath = value.ToString();
                    await SendAppropriateWelcomeEmailAsync(user, initiationPath);
                    if (!string.IsNullOrEmpty(initiationPath))
                    {
                        await _referenceEventService.RaiseEventAsync(
                            new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext)
                            {
                                SignupInitiationPath = initiationPath
                            });

                        return result;
                    }
                }
            }

            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext));
        }

        return result;
    }

    private void ValidateOrgInviteToken(string orgInviteToken, Guid? orgUserId, User user)
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
    private async Task SetUserEmail2FaIfOrgPolicyEnabledAsync(Guid? orgUserId, User user)
    {
        if (!orgUserId.HasValue)
        {
            return;
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
    }


    private async Task SendAppropriateWelcomeEmailAsync(User user, string initiationPath)
    {
        var isFromMarketingWebsite = initiationPath.Contains("Secrets Manager trial");

        if (isFromMarketingWebsite)
        {
            await _mailService.SendTrialInitiationEmailAsync(user.Email);
        }
        else
        {
            await _mailService.SendWelcomeEmailAsync(user);
        }
    }

    public async Task<IdentityResult> RegisterUserViaEmailVerificationToken(User user, string masterPasswordHash,
        string emailVerificationToken)
    {

        ValidateOpenRegistrationAllowed();

        var tokenable = ValidateRegistrationEmailVerificationTokenable(emailVerificationToken, user.Email);

        user.EmailVerified = true;
        user.Name = tokenable.Name;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await _mailService.SendWelcomeEmailAsync(user);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext)
            {
                ReceiveMarketingEmails = tokenable.ReceiveMarketingEmails
            });
        }

        return result;
    }

    public async Task<IdentityResult> RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(User user, string masterPasswordHash,
        string orgSponsoredFreeFamilyPlanInviteToken)
    {
        ValidateOpenRegistrationAllowed();
        await ValidateOrgSponsoredFreeFamilyPlanInviteToken(orgSponsoredFreeFamilyPlanInviteToken, user.Email);

        user.EmailVerified = true;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await _mailService.SendWelcomeEmailAsync(user);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext));
        }

        return result;
    }


    // TODO: in future, consider how we can consolidate base registration logic to reduce code duplication
    public async Task<IdentityResult> RegisterUserViaAcceptEmergencyAccessInviteToken(User user, string masterPasswordHash,
        string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        ValidateOpenRegistrationAllowed();
        ValidateAcceptEmergencyAccessInviteToken(acceptEmergencyAccessInviteToken, acceptEmergencyAccessId, user.Email);

        user.EmailVerified = true;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await _mailService.SendWelcomeEmailAsync(user);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext));
        }

        return result;
    }

    public async Task<IdentityResult> RegisterUserViaProviderInviteToken(User user, string masterPasswordHash,
        string providerInviteToken, Guid providerUserId)
    {
        ValidateOpenRegistrationAllowed();
        ValidateProviderInviteToken(providerInviteToken, providerUserId, user.Email);

        user.EmailVerified = true;
        user.ApiKey = CoreHelpers.SecureRandomString(30); // API key can't be null.

        var result = await _userService.CreateUserAsync(user, masterPasswordHash);
        if (result == IdentityResult.Success)
        {
            await _mailService.SendWelcomeEmailAsync(user);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.Signup, user, _currentContext));
        }

        return result;
    }

    private void ValidateOpenRegistrationAllowed()
    {
        // We validate open registration on send of initial email and here b/c a user could technically start the
        // account creation process while open registration is enabled and then finish it after it has been
        // disabled by the self hosted admin.Ï
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
}
