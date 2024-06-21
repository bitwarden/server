using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
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

    private readonly ICurrentContext _currentContext;

    private readonly IUserService _userService;
    private readonly IMailService _mailService;

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
        IMailService mailService
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


    public async Task<IdentityResult> RegisterUserWithOptionalOrgInvite(User user, string masterPasswordHash,
        string orgInviteToken, Guid? orgUserId)
    {
        ValidateOrgInviteToken(orgInviteToken, orgUserId, user);
        await SetUserEmail2FaIfOrgPolicyEnabledAsync(orgUserId, user);

        user.ApiKey = CoreHelpers.SecureRandomString(30);
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
        // If open registration is disabled and there isn't an org invite token, then throw an exception
        if (_globalSettings.DisableUserRegistration && string.IsNullOrWhiteSpace(orgInviteToken) && !orgUserId.HasValue)
        {
            throw new BadRequestException("Open registration has been disabled by the system administrator.");
        }

        // if user has an org invite token but not org user id, then throw an exception as we can't validate the token
        // The web client should always send both orgInviteToken and orgUserId together.
        if (!string.IsNullOrWhiteSpace(orgInviteToken) && !orgUserId.HasValue)
        {
            throw new BadRequestException("Organization invite token cannot be validated without an organization user id.");
        }

        // TODO: determine if we should throw if we have an org user id but no org invite token. It's technically possible
        // but doesn't seem to be a supported flow right now.

        // if we have an org invite token but it is invalid, then throw an exception
        if (!string.IsNullOrWhiteSpace(orgInviteToken) && orgUserId.HasValue && !IsOrgInviteTokenValid(orgInviteToken, orgUserId.Value, user.Email))
        {
            throw new BadRequestException("Organization invite token is invalid.");
        }

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
    /// Handles initializing the user to have Email 2FA enabled if they are subject to an enabled 2FA organizational policy.
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
}
