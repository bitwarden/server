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

    public async Task<IdentityResult> RegisterUserViaOrganizationInvite(User user, string masterPasswordHash,
        string orgInviteToken, Guid? orgUserId)
    {
        var orgInviteTokenValid = false;
        if (_globalSettings.DisableUserRegistration && !string.IsNullOrWhiteSpace(orgInviteToken) && orgUserId.HasValue)
        {
            // TODO: PM-4142 - remove old token validation logic once 3 releases of backwards compatibility are complete
            var newOrgInviteTokenValid = OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
                _orgUserInviteTokenDataFactory, orgInviteToken, orgUserId.Value, user.Email);

            orgInviteTokenValid = newOrgInviteTokenValid ||
                                  CoreHelpers.UserInviteTokenIsValid(_organizationServiceDataProtector, orgInviteToken,
                                      user.Email, orgUserId.Value, _globalSettings);
        }

        if (_globalSettings.DisableUserRegistration && !orgInviteTokenValid)
        {
            throw new BadRequestException("Open registration has been disabled by the system administrator.");
        }

        if (orgUserId.HasValue)
        {
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
