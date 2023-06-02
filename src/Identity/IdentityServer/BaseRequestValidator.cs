using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Response;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using Duo = DuoUniversal;

namespace Bit.Identity.IdentityServer;

public abstract class BaseRequestValidator<T> where T : class
{
    private UserManager<User> _userManager;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceService _deviceService;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IOrganizationDuoWebTokenProvider _organizationDuoWebTokenProvider;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IMailService _mailService;
    private readonly ILogger _logger;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IPolicyService _policyService;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> _tokenDataFactory;

    public BaseRequestValidator(
        UserManager<User> userManager,
        IDeviceRepository deviceRepository,
        IDeviceService deviceService,
        IUserService userService,
        IEventService eventService,
        IOrganizationDuoWebTokenProvider organizationDuoWebTokenProvider,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService,
        IMailService mailService,
        ILogger logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IPolicyRepository policyRepository,
        IUserRepository userRepository,
        IPolicyService policyService,
        IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> tokenDataFactory)
    {
        _userManager = userManager;
        _deviceRepository = deviceRepository;
        _deviceService = deviceService;
        _userService = userService;
        _eventService = eventService;
        _organizationDuoWebTokenProvider = organizationDuoWebTokenProvider;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _applicationCacheService = applicationCacheService;
        _mailService = mailService;
        _logger = logger;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _policyService = policyService;
        _userRepository = userRepository;
        _policyService = policyService;
        _tokenDataFactory = tokenDataFactory;
    }

    protected async Task ValidateAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        var isBot = (validatorContext.CaptchaResponse?.IsBot ?? false);
        if (isBot)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Login attempt for {0} detected as a captcha bot with score {1}.",
                request.UserName, validatorContext.CaptchaResponse.Score);
        }

        var twoFactorToken = request.Raw["TwoFactorToken"]?.ToString();
        var twoFactorProvider = request.Raw["TwoFactorProvider"]?.ToString();
        var twoFactorRemember = request.Raw["TwoFactorRemember"]?.ToString() == "1";
        var twoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) &&
                               !string.IsNullOrWhiteSpace(twoFactorProvider);

        var valid = await ValidateContextAsync(context, validatorContext);
        var user = validatorContext.User;
        if (!valid)
        {
            await UpdateFailedAuthDetailsAsync(user, false, !validatorContext.KnownDevice);
        }

        if (!valid || isBot)
        {
            await BuildErrorResultAsync("Username or password is incorrect. Try again.", false, context, user);
            return;
        }

        var (isTwoFactorRequired, twoFactorOrganization) = await RequiresTwoFactorAsync(user, request);
        if (isTwoFactorRequired)
        {
            // Just defaulting it
            var twoFactorProviderType = TwoFactorProviderType.Authenticator;
            if (!twoFactorRequest || !Enum.TryParse(twoFactorProvider, out twoFactorProviderType))
            {
                await BuildTwoFactorResultAsync(user, twoFactorOrganization, context);
                return;
            }

            var verified = await VerifyTwoFactor(user, twoFactorOrganization,
                twoFactorProviderType, twoFactorToken);

            if ((!verified || isBot) && twoFactorProviderType != TwoFactorProviderType.Remember)
            {
                await UpdateFailedAuthDetailsAsync(user, true, !validatorContext.KnownDevice);
                await BuildErrorResultAsync("Two-step token is invalid. Try again.", true, context, user);
                return;
            }
            else if ((!verified || isBot) && twoFactorProviderType == TwoFactorProviderType.Remember)
            {
                // Delay for brute force.
                await Task.Delay(2000);
                await BuildTwoFactorResultAsync(user, twoFactorOrganization, context);
                return;
            }
        }
        else
        {
            twoFactorRequest = false;
            twoFactorRemember = false;
            twoFactorToken = null;
        }

        // Returns true if can finish validation process
        if (await IsValidAuthTypeAsync(user, request.GrantType))
        {
            var device = await SaveDeviceAsync(user, request);
            if (device == null)
            {
                await BuildErrorResultAsync("No device information provided.", false, context, user);
                return;
            }

            await BuildSuccessResultAsync(user, context, device, twoFactorRequest && twoFactorRemember);
        }
        else
        {
            SetSsoResult(context,
                new Dictionary<string, object>
                {
                    { "ErrorModel", new ErrorResponseModel("SSO authentication is required.") }
                });
        }
    }

    protected abstract Task<bool> ValidateContextAsync(T context, CustomValidatorRequestContext validatorContext);

    protected async Task BuildSuccessResultAsync(User user, T context, Device device, bool sendRememberToken)
    {
        await _eventService.LogUserEventAsync(user.Id, EventType.User_LoggedIn);

        var claims = new List<Claim>();

        if (device != null)
        {
            claims.Add(new Claim(Claims.Device, device.Identifier));
        }

        var customResponse = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(user.PrivateKey))
        {
            customResponse.Add("PrivateKey", user.PrivateKey);
        }

        if (!string.IsNullOrWhiteSpace(user.Key))
        {
            customResponse.Add("Key", user.Key);
        }

        customResponse.Add("MasterPasswordPolicy", await GetMasterPasswordPolicy(user));
        customResponse.Add("ForcePasswordReset", user.ForcePasswordReset);
        customResponse.Add("ResetMasterPassword", string.IsNullOrWhiteSpace(user.MasterPassword));
        customResponse.Add("Kdf", (byte)user.Kdf);
        customResponse.Add("KdfIterations", user.KdfIterations);
        customResponse.Add("KdfMemory", user.KdfMemory);
        customResponse.Add("KdfParallelism", user.KdfParallelism);

        if (sendRememberToken)
        {
            var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Remember));
            customResponse.Add("TwoFactorToken", token);
        }

        await ResetFailedAuthDetailsAsync(user);
        await SetSuccessResult(context, user, claims, customResponse);
    }

    protected async Task BuildTwoFactorResultAsync(User user, Organization organization, T context)
    {
        var providerKeys = new List<byte>();
        var providers = new Dictionary<string, Dictionary<string, object>>();

        var enabledProviders = new List<KeyValuePair<TwoFactorProviderType, TwoFactorProvider>>();
        if (organization?.GetTwoFactorProviders() != null)
        {
            enabledProviders.AddRange(organization.GetTwoFactorProviders().Where(
                p => organization.TwoFactorProviderIsEnabled(p.Key)));
        }

        if (user.GetTwoFactorProviders() != null)
        {
            foreach (var p in user.GetTwoFactorProviders())
            {
                if (await _userService.TwoFactorProviderIsEnabledAsync(p.Key, user))
                {
                    enabledProviders.Add(p);
                }
            }
        }

        if (!enabledProviders.Any())
        {
            await BuildErrorResultAsync("No two-step providers enabled.", false, context, user);
            return;
        }

        foreach (var provider in enabledProviders)
        {
            providerKeys.Add((byte)provider.Key);
            var infoDict = await BuildTwoFactorParams(organization, user, provider.Key, provider.Value);
            providers.Add(((byte)provider.Key).ToString(), infoDict);
        }

        var twoFactorResultDict = new Dictionary<string, object>
        {
            { "TwoFactorProviders", providers.Keys },
            { "TwoFactorProviders2", providers },
            { "MasterPasswordPolicy", await GetMasterPasswordPolicy(user) },
        };

        // If we have email as a 2FA provider, we might need an SsoEmail2fa Session Token
        if (enabledProviders.Any(p => p.Key == TwoFactorProviderType.Email))
        {
            twoFactorResultDict.Add("SsoEmail2faSessionToken",
                _tokenDataFactory.Protect(new SsoEmail2faSessionTokenable(user)));

            twoFactorResultDict.Add("Email", user.Email);
        }

        SetTwoFactorResult(context, twoFactorResultDict);

        if (enabledProviders.Count() == 1 && enabledProviders.First().Key == TwoFactorProviderType.Email)
        {
            // Send email now if this is their only 2FA method
            await _userService.SendTwoFactorEmailAsync(user);
        }
    }

    protected async Task BuildErrorResultAsync(string message, bool twoFactorRequest, T context, User user)
    {
        if (user != null)
        {
            await _eventService.LogUserEventAsync(user.Id,
                twoFactorRequest ? EventType.User_FailedLogIn2fa : EventType.User_FailedLogIn);
        }

        if (_globalSettings.SelfHosted)
        {
            _logger.LogWarning(Constants.BypassFiltersEventId,
                string.Format("Failed login attempt{0}{1}", twoFactorRequest ? ", 2FA invalid." : ".",
                    $" {_currentContext.IpAddress}"));
        }

        await Task.Delay(2000); // Delay for brute force.
        SetErrorResult(context,
            new Dictionary<string, object> { { "ErrorModel", new ErrorResponseModel(message) } });
    }

    protected abstract void SetTwoFactorResult(T context, Dictionary<string, object> customResponse);

    protected abstract void SetSsoResult(T context, Dictionary<string, object> customResponse);

    protected abstract Task SetSuccessResult(T context, User user, List<Claim> claims,
        Dictionary<string, object> customResponse);

    protected abstract void SetErrorResult(T context, Dictionary<string, object> customResponse);

    private async Task<Tuple<bool, Organization>> RequiresTwoFactorAsync(User user, ValidatedTokenRequest request)
    {
        if (request.GrantType == "client_credentials")
        {
            // Do not require MFA for api key logins
            return new Tuple<bool, Organization>(false, null);
        }

        var individualRequired = _userManager.SupportsUserTwoFactor &&
                                 await _userManager.GetTwoFactorEnabledAsync(user) &&
                                 (await _userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;

        Organization firstEnabledOrg = null;
        var orgs = (await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id))
            .ToList();
        if (orgs.Any())
        {
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            var twoFactorOrgs = orgs.Where(o => OrgUsing2fa(orgAbilities, o.Id));
            if (twoFactorOrgs.Any())
            {
                var userOrgs = await _organizationRepository.GetManyByUserIdAsync(user.Id);
                firstEnabledOrg = userOrgs.FirstOrDefault(
                    o => orgs.Any(om => om.Id == o.Id) && o.TwoFactorIsEnabled());
            }
        }

        return new Tuple<bool, Organization>(individualRequired || firstEnabledOrg != null, firstEnabledOrg);
    }

    private async Task<bool> IsValidAuthTypeAsync(User user, string grantType)
    {
        if (grantType == "authorization_code" || grantType == "client_credentials")
        {
            // Already using SSO to authorize, finish successfully
            // Or login via api key, skip SSO requirement
            return true;
        }

        // Check if user belongs to any organization with an active SSO policy 
        var anySsoPoliciesApplicableToUser = await _policyService.AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        if (anySsoPoliciesApplicableToUser)
        {
            return false;
        }

        // Default - continue validation process
        return true;
    }

    private bool OrgUsing2fa(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
    {
        return orgAbilities != null && orgAbilities.ContainsKey(orgId) &&
               orgAbilities[orgId].Enabled && orgAbilities[orgId].Using2fa;
    }

    private Device GetDeviceFromRequest(ValidatedRequest request)
    {
        var deviceIdentifier = request.Raw["DeviceIdentifier"]?.ToString();
        var deviceType = request.Raw["DeviceType"]?.ToString();
        var deviceName = request.Raw["DeviceName"]?.ToString();
        var devicePushToken = request.Raw["DevicePushToken"]?.ToString();

        if (string.IsNullOrWhiteSpace(deviceIdentifier) || string.IsNullOrWhiteSpace(deviceType) ||
            string.IsNullOrWhiteSpace(deviceName) || !Enum.TryParse(deviceType, out DeviceType type))
        {
            return null;
        }

        return new Device
        {
            Identifier = deviceIdentifier,
            Name = deviceName,
            Type = type,
            PushToken = string.IsNullOrWhiteSpace(devicePushToken) ? null : devicePushToken
        };
    }

    private async Task<bool> VerifyTwoFactor(User user, Organization organization, TwoFactorProviderType type,
        string token)
    {
        switch (type)
        {
            case TwoFactorProviderType.Authenticator:
            case TwoFactorProviderType.Email:
            case TwoFactorProviderType.Duo:
            case TwoFactorProviderType.YubiKey:
            case TwoFactorProviderType.WebAuthn:
            case TwoFactorProviderType.Remember:
                if (type != TwoFactorProviderType.Remember &&
                    !(await _userService.TwoFactorProviderIsEnabledAsync(type, user)))
                {
                    return false;
                }

                return await _userManager.VerifyTwoFactorTokenAsync(user,
                    CoreHelpers.CustomProviderName(type), token);
            case TwoFactorProviderType.OrganizationDuo:
                if (!organization?.TwoFactorProviderIsEnabled(type) ?? true)
                {
                    return false;
                }

                return await _organizationDuoWebTokenProvider.ValidateAsync(token, organization, user);
            default:
                return false;
        }
    }

    private async Task<Dictionary<string, object>> BuildTwoFactorParams(Organization organization, User user,
        TwoFactorProviderType type, TwoFactorProvider provider)
    {
        switch (type)
        {
            case TwoFactorProviderType.Duo:
            case TwoFactorProviderType.WebAuthn:
            case TwoFactorProviderType.Email:
            case TwoFactorProviderType.YubiKey:
                if (!(await _userService.TwoFactorProviderIsEnabledAsync(type, user)))
                {
                    return null;
                }

                var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                    CoreHelpers.CustomProviderName(type));
                if (type == TwoFactorProviderType.Duo)
                {
                    string clientId = "-------------";
                    string clientSecret = "-------------";
                    string apiHost = "-------------";
                    string returnUrl = "-----------------------";
                    Duo.Client duoClient = new Duo.ClientBuilder(clientId, clientSecret, apiHost, returnUrl).Build();

                    // Check if Duo seems to be healthy and able to service authentications.
                    // If Duo were unhealthy, you could possibly send user to an error page, or implement a fail mode
                    var isDuoHealthy = await duoClient.DoHealthCheck();

                    // Generate a random state value to tie the authentication steps together
                    string state = Duo.Client.GenerateState();
                    // Save the state and username in the session for later
                    // _currentContext.HttpContext.Session.SetString(STATE_SESSION_KEY, state);
                    // HttpContext.Session.SetString(USERNAME_SESSION_KEY, username);

                    // Get the URI of the Duo prompt from the client.  This includes an embedded authentication request.
                    string promptUri = duoClient.GenerateAuthUri("jfink@bitwarden.com", state);
                    return new Dictionary<string, object>
                    {
                        // ["Host"] = provider.MetaData["Host"],
                        ["Host"] = promptUri,
                        ["Signature"] = token
                    };
                }
                else if (type == TwoFactorProviderType.WebAuthn)
                {
                    if (token == null)
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<Dictionary<string, object>>(token);
                }
                else if (type == TwoFactorProviderType.Email)
                {
                    return new Dictionary<string, object> { ["Email"] = token };
                }
                else if (type == TwoFactorProviderType.YubiKey)
                {
                    return new Dictionary<string, object> { ["Nfc"] = (bool)provider.MetaData["Nfc"] };
                }

                return null;
            case TwoFactorProviderType.OrganizationDuo:
                if (await _organizationDuoWebTokenProvider.CanGenerateTwoFactorTokenAsync(organization))
                {
                    return new Dictionary<string, object>
                    {
                        ["Host"] = provider.MetaData["Host"],
                        ["Signature"] = await _organizationDuoWebTokenProvider.GenerateAsync(organization, user)
                    };
                }

                return null;
            default:
                return null;
        }
    }

    protected async Task<bool> KnownDeviceAsync(User user, ValidatedTokenRequest request) =>
        (await GetKnownDeviceAsync(user, request)) != default;

    protected async Task<Device> GetKnownDeviceAsync(User user, ValidatedTokenRequest request)
    {
        if (user == null)
        {
            return default;
        }

        return await _deviceRepository.GetByIdentifierAsync(GetDeviceFromRequest(request).Identifier, user.Id);
    }

    private async Task<Device> SaveDeviceAsync(User user, ValidatedTokenRequest request)
    {
        var device = GetDeviceFromRequest(request);
        if (device != null)
        {
            var existingDevice = await GetKnownDeviceAsync(user, request);
            if (existingDevice == null)
            {
                device.UserId = user.Id;
                await _deviceService.SaveAsync(device);

                var now = DateTime.UtcNow;
                if (now - user.CreationDate > TimeSpan.FromMinutes(10))
                {
                    var deviceType = device.Type.GetType().GetMember(device.Type.ToString())
                        .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName();
                    if (!_globalSettings.DisableEmailNewDevice)
                    {
                        await _mailService.SendNewDeviceLoggedInEmail(user.Email, deviceType, now,
                            _currentContext.IpAddress);
                    }
                }

                return device;
            }

            return existingDevice;
        }

        return null;
    }

    private async Task ResetFailedAuthDetailsAsync(User user)
    {
        // Early escape if db hit not necessary
        if (user == null || user.FailedLoginCount == 0)
        {
            return;
        }

        user.FailedLoginCount = 0;
        user.RevisionDate = DateTime.UtcNow;
        await _userRepository.ReplaceAsync(user);
    }

    private async Task UpdateFailedAuthDetailsAsync(User user, bool twoFactorInvalid, bool unknownDevice)
    {
        if (user == null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        user.FailedLoginCount = ++user.FailedLoginCount;
        user.LastFailedLoginDate = user.RevisionDate = utcNow;
        await _userRepository.ReplaceAsync(user);

        if (ValidateFailedAuthEmailConditions(unknownDevice, user))
        {
            if (twoFactorInvalid)
            {
                await _mailService.SendFailedTwoFactorAttemptsEmailAsync(user.Email, utcNow, _currentContext.IpAddress);
            }
            else
            {
                await _mailService.SendFailedLoginAttemptsEmailAsync(user.Email, utcNow, _currentContext.IpAddress);
            }
        }
    }

    private bool ValidateFailedAuthEmailConditions(bool unknownDevice, User user)
    {
        var failedLoginCeiling = _globalSettings.Captcha.MaximumFailedLoginAttempts;
        var failedLoginCount = user?.FailedLoginCount ?? 0;
        return unknownDevice && failedLoginCeiling > 0 && failedLoginCount == failedLoginCeiling;
    }

    private async Task<MasterPasswordPolicyResponseModel> GetMasterPasswordPolicy(User user)
    {
        // Check current context/cache to see if user is in any organizations, avoids extra DB call if not
        var orgs = (await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id))
            .ToList();

        if (!orgs.Any())
        {
            return null;
        }

        return new MasterPasswordPolicyResponseModel(await _policyService.GetMasterPasswordPolicyForUserAsync(user));
    }
}
