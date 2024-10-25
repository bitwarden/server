using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Response;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public abstract class BaseRequestValidator<T> where T : class
{
    private UserManager<User> _userManager;
    private readonly IEventService _eventService;
    private readonly IDeviceValidator _deviceValidator;
    private readonly ITwoFactorAuthenticationValidator _twoFactorAuthenticationValidator;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly ILogger _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;

    protected ICurrentContext CurrentContext { get; }
    protected IPolicyService PolicyService { get; }
    protected IFeatureService FeatureService { get; }
    protected ISsoConfigRepository SsoConfigRepository { get; }
    protected IUserService _userService { get; }
    protected IUserDecryptionOptionsBuilder UserDecryptionOptionsBuilder { get; }

    public BaseRequestValidator(
        UserManager<User> userManager,
        IUserService userService,
        IEventService eventService,
        IDeviceValidator deviceValidator,
        ITwoFactorAuthenticationValidator twoFactorAuthenticationValidator,
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        ILogger logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IUserRepository userRepository,
        IPolicyService policyService,
        IFeatureService featureService,
        ISsoConfigRepository ssoConfigRepository,
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder)
    {
        _userManager = userManager;
        _userService = userService;
        _eventService = eventService;
        _deviceValidator = deviceValidator;
        _twoFactorAuthenticationValidator = twoFactorAuthenticationValidator;
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _logger = logger;
        CurrentContext = currentContext;
        _globalSettings = globalSettings;
        PolicyService = policyService;
        _userRepository = userRepository;
        FeatureService = featureService;
        SsoConfigRepository = ssoConfigRepository;
        UserDecryptionOptionsBuilder = userDecryptionOptionsBuilder;
    }

    protected async Task ValidateAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        var isBot = validatorContext.CaptchaResponse?.IsBot ?? false;
        if (isBot)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Login attempt for {0} detected as a captcha bot with score {1}.",
                request.UserName, validatorContext.CaptchaResponse.Score);
        }

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

        var (isTwoFactorRequired, twoFactorOrganization) = await _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(user, request);
        var twoFactorToken = request.Raw["TwoFactorToken"]?.ToString();
        var twoFactorProvider = request.Raw["TwoFactorProvider"]?.ToString();
        var twoFactorRemember = request.Raw["TwoFactorRemember"]?.ToString() == "1";
        var validTwoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) &&
                                    !string.IsNullOrWhiteSpace(twoFactorProvider);

        if (isTwoFactorRequired)
        {
            // 2FA required and not provided response
            if (!validTwoFactorRequest ||
                !Enum.TryParse(twoFactorProvider, out TwoFactorProviderType twoFactorProviderType))
            {
                var resultDict = await _twoFactorAuthenticationValidator
                                        .BuildTwoFactorResultAsync(user, twoFactorOrganization);
                if (resultDict == null)
                {
                    await BuildErrorResultAsync("No two-step providers enabled.", false, context, user);
                    return;
                }

                // Include Master Password Policy in 2FA response
                resultDict.Add("MasterPasswordPolicy", await GetMasterPasswordPolicy(user));
                SetTwoFactorResult(context, resultDict);
                return;
            }

            var verified = await _twoFactorAuthenticationValidator
                                    .VerifyTwoFactor(user, twoFactorOrganization, twoFactorProviderType, twoFactorToken);

            // 2FA required but request not valid or remember token expired response
            if (!verified || isBot)
            {
                if (twoFactorProviderType != TwoFactorProviderType.Remember)
                {
                    await UpdateFailedAuthDetailsAsync(user, true, !validatorContext.KnownDevice);
                    await BuildErrorResultAsync("Two-step token is invalid. Try again.", true, context, user);
                }
                else if (twoFactorProviderType == TwoFactorProviderType.Remember)
                {
                    var resultDict = await _twoFactorAuthenticationValidator
                                            .BuildTwoFactorResultAsync(user, twoFactorOrganization);

                    // Include Master Password Policy in 2FA response
                    resultDict.Add("MasterPasswordPolicy", await GetMasterPasswordPolicy(user));
                    SetTwoFactorResult(context, resultDict);
                }
                return;
            }
        }
        else
        {
            validTwoFactorRequest = false;
            twoFactorRemember = false;
        }

        // Force legacy users to the web for migration
        if (FeatureService.IsEnabled(FeatureFlagKeys.BlockLegacyUsers))
        {
            if (UserService.IsLegacyUser(user) && request.ClientId != "web")
            {
                await FailAuthForLegacyUserAsync(user, context);
                return;
            }
        }

        if (await IsValidAuthTypeAsync(user, request.GrantType))
        {
            var device = await _deviceValidator.SaveDeviceAsync(user, request);
            if (device == null)
            {
                await BuildErrorResultAsync("No device information provided.", false, context, user);
                return;
            }
            await BuildSuccessResultAsync(user, context, device, validTwoFactorRequest && twoFactorRemember);
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

    protected async Task FailAuthForLegacyUserAsync(User user, T context)
    {
        await BuildErrorResultAsync(
            $"Encryption key migration is required. Please log in to the web vault at {_globalSettings.BaseServiceUri.VaultWithHash}",
            false, context, user);
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
        customResponse.Add("UserDecryptionOptions", await CreateUserDecryptionOptionsAsync(user, device, GetSubject(context)));

        if (sendRememberToken)
        {
            var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Remember));
            customResponse.Add("TwoFactorToken", token);
        }

        await ResetFailedAuthDetailsAsync(user);
        await SetSuccessResult(context, user, claims, customResponse);
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
                    $" {CurrentContext.IpAddress}"));
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
    protected abstract ClaimsPrincipal GetSubject(T context);

    /// <summary>
    /// Check if the user is required to authenticate via SSO. If the user requires SSO, but they are
    /// logging in using an API Key (client_credentials) then they are allowed to bypass the SSO requirement.
    /// </summary>
    /// <param name="user">user trying to login</param>
    /// <param name="grantType">magic string identifying the grant type requested</param>
    /// <returns></returns>
    private async Task<bool> IsValidAuthTypeAsync(User user, string grantType)
    {
        if (grantType == "authorization_code" || grantType == "client_credentials")
        {
            // Already using SSO to authorize, finish successfully
            // Or login via api key, skip SSO requirement
            return true;
        }

        // Check if user belongs to any organization with an active SSO policy
        var anySsoPoliciesApplicableToUser = await PolicyService.AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        if (anySsoPoliciesApplicableToUser)
        {
            return false;
        }

        // Default - continue validation process
        return true;
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
                await _mailService.SendFailedTwoFactorAttemptsEmailAsync(user.Email, utcNow, CurrentContext.IpAddress);
            }
            else
            {
                await _mailService.SendFailedLoginAttemptsEmailAsync(user.Email, utcNow, CurrentContext.IpAddress);
            }
        }
    }

    /// <summary>
    /// checks to see if a user is trying to log into a new device
    /// and has reached the maximum number of failed login attempts.
    /// </summary>
    /// <param name="unknownDevice">boolean</param>
    /// <param name="user">current user</param>
    /// <returns></returns>
    private bool ValidateFailedAuthEmailConditions(bool unknownDevice, User user)
    {
        var failedLoginCeiling = _globalSettings.Captcha.MaximumFailedLoginAttempts;
        var failedLoginCount = user?.FailedLoginCount ?? 0;
        return unknownDevice && failedLoginCeiling > 0 && failedLoginCount == failedLoginCeiling;
    }

    private async Task<MasterPasswordPolicyResponseModel> GetMasterPasswordPolicy(User user)
    {
        // Check current context/cache to see if user is in any organizations, avoids extra DB call if not
        var orgs = (await CurrentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id))
            .ToList();

        if (!orgs.Any())
        {
            return null;
        }

        return new MasterPasswordPolicyResponseModel(await PolicyService.GetMasterPasswordPolicyForUserAsync(user));
    }

#nullable enable
    /// <summary>
    /// Used to create a list of all possible ways the newly authenticated user can decrypt their vault contents
    /// </summary>
    private async Task<UserDecryptionOptions> CreateUserDecryptionOptionsAsync(User user, Device device, ClaimsPrincipal subject)
    {
        var ssoConfig = await GetSsoConfigurationDataAsync(subject);
        return await UserDecryptionOptionsBuilder
            .ForUser(user)
            .WithDevice(device)
            .WithSso(ssoConfig)
            .BuildAsync();
    }

    private async Task<SsoConfig?> GetSsoConfigurationDataAsync(ClaimsPrincipal subject)
    {
        var organizationClaim = subject?.FindFirstValue("organizationId");

        if (organizationClaim == null || !Guid.TryParse(organizationClaim, out var organizationId))
        {
            return null;
        }

        var ssoConfig = await SsoConfigRepository.GetByOrganizationIdAsync(organizationId);
        if (ssoConfig == null)
        {
            return null;
        }

        return ssoConfig;
    }
}
