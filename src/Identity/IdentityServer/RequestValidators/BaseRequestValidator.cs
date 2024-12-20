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
using Bit.Core.Resources;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

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
    private readonly IStringLocalizer _errorStringLocalizer;

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
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder,
        IStringLocalizerFactory stringLocalizerFactory)
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

        _errorStringLocalizer = stringLocalizerFactory.CreateLocalizer<ErrorMessages>();
    }

    protected async Task ValidateAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        // 1. we need to check if the user is a bot and if their master password hash is correct
        var isBot = validatorContext.CaptchaResponse?.IsBot ?? false;
        var valid = await ValidateContextAsync(context, validatorContext);
        var user = validatorContext.User;
        if (!valid || isBot)
        {
            if (isBot)
            {
                _logger.LogInformation(Constants.BypassFiltersEventId,
                    "Login attempt for {UserName} detected as a captcha bot with score {CaptchaScore}.",
                    request.UserName, validatorContext.CaptchaResponse.Score);
            }

            if (!valid)
            {
                await UpdateFailedAuthDetailsAsync(user, false, !validatorContext.KnownDevice);
            }

            await BuildErrorResultAsync(_errorStringLocalizer[ErrorCodes.IDENTITY_INVALID_USERNAME_OR_PASSWORD], false, context, user);
            return;
        }

        // 2. Does this user belong to an organization that requires SSO
        validatorContext.SsoRequired = await RequireSsoLoginAsync(user, request.GrantType);
        if (validatorContext.SsoRequired)
        {
            SetSsoResult(context,
                new Dictionary<string, object>
                {
                    { "ErrorModel", new ErrorResponseModel(_errorStringLocalizer[ErrorCodes.IDENTITY_SSO_REQUIRED]) }
                });
            return;
        }

        // 3. Check if 2FA is required
        (validatorContext.TwoFactorRequired, var twoFactorOrganization) = await _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(user, request);
        // This flag is used to determine if the user wants a rememberMe token sent when authentication is successful
        var returnRememberMeToken = false;
        if (validatorContext.TwoFactorRequired)
        {
            var twoFactorToken = request.Raw["TwoFactorToken"]?.ToString();
            var twoFactorProvider = request.Raw["TwoFactorProvider"]?.ToString();
            var validTwoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) &&
                                        !string.IsNullOrWhiteSpace(twoFactorProvider);
            // response for 2FA required and not provided state
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

            var twoFactorTokenValid = await _twoFactorAuthenticationValidator
                                    .VerifyTwoFactor(user, twoFactorOrganization, twoFactorProviderType, twoFactorToken);

            // response for 2FA required but request is not valid or remember token expired state
            if (!twoFactorTokenValid)
            {
                // The remember me token has expired
                if (twoFactorProviderType == TwoFactorProviderType.Remember)
                {
                    var resultDict = await _twoFactorAuthenticationValidator
                                            .BuildTwoFactorResultAsync(user, twoFactorOrganization);

                    // Include Master Password Policy in 2FA response
                    resultDict.Add("MasterPasswordPolicy", await GetMasterPasswordPolicy(user));
                    SetTwoFactorResult(context, resultDict);
                }
                else
                {
                    await UpdateFailedAuthDetailsAsync(user, true, !validatorContext.KnownDevice);
                    await BuildErrorResultAsync("Two-step token is invalid. Try again.", true, context, user);
                }
                return;
            }

            // When the two factor authentication is successful, we can check if the user wants a rememberMe token
            var twoFactorRemember = request.Raw["TwoFactorRemember"]?.ToString() == "1";
            if (twoFactorRemember // Check if the user wants a rememberMe token
                && twoFactorTokenValid // Make sure two factor authentication was successful
                && twoFactorProviderType != TwoFactorProviderType.Remember) // if the two factor auth was rememberMe do not send another token
            {
                returnRememberMeToken = true;
            }
        }

        // 4. Check if the user is logging in from a new device
        var deviceValid = await _deviceValidator.ValidateRequestDeviceAsync(request, validatorContext);
        if (!deviceValid)
        {
            SetValidationErrorResult(context, validatorContext);
            await LogFailedLoginEvent(validatorContext.User, EventType.User_FailedLogIn);
            return;
        }

        // 5. Force legacy users to the web for migration
        if (FeatureService.IsEnabled(FeatureFlagKeys.BlockLegacyUsers))
        {
            if (UserService.IsLegacyUser(user) && request.ClientId != "web")
            {
                await FailAuthForLegacyUserAsync(user, context);
                return;
            }
        }

        await BuildSuccessResultAsync(user, context, validatorContext.Device, returnRememberMeToken);
    }

    protected async Task FailAuthForLegacyUserAsync(User user, T context)
    {
        await BuildErrorResultAsync(
            $"{_errorStringLocalizer[ErrorCodes.IDENTITY_ENCRYPTION_KEY_MIGRATION_REQUIRED]} {_globalSettings.BaseServiceUri.VaultWithHash}",
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

    /// <summary>
    /// This does two things, it sets the error result for the current ValidatorContext _and_ it logs error.
    /// These two things should be seperated to maintain single concerns.
    /// </summary>
    /// <param name="message">Error message for the error result</param>
    /// <param name="twoFactorRequest">bool that controls how the error is logged</param>
    /// <param name="context">used to set the error result in the current validator</param>
    /// <param name="user">used to associate the failed login with a user</param>
    /// <returns>void</returns>
    [Obsolete("Consider using SetValidationErrorResult to set the validation result, and LogFailedLoginEvent " +
        "to log the failure.")]
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

    protected async Task LogFailedLoginEvent(User user, EventType eventType)
    {
        if (user != null)
        {
            await _eventService.LogUserEventAsync(user.Id, eventType);
        }

        if (_globalSettings.SelfHosted)
        {
            string formattedMessage;
            switch (eventType)
            {
                case EventType.User_FailedLogIn:
                    formattedMessage = string.Format("Failed login attempt. {0}", $" {CurrentContext.IpAddress}");
                    break;
                case EventType.User_FailedLogIn2fa:
                    formattedMessage = string.Format("Failed login attempt, 2FA invalid.{0}", $" {CurrentContext.IpAddress}");
                    break;
                default:
                    formattedMessage = "Failed login attempt.";
                    break;
            }
            _logger.LogWarning(Constants.BypassFiltersEventId, formattedMessage);
        }
        await Task.Delay(2000); // Delay for brute force.
    }

    [Obsolete("Consider using SetValidationErrorResult instead.")]
    protected abstract void SetTwoFactorResult(T context, Dictionary<string, object> customResponse);
    [Obsolete("Consider using SetValidationErrorResult instead.")]
    protected abstract void SetSsoResult(T context, Dictionary<string, object> customResponse);
    [Obsolete("Consider using SetValidationErrorResult instead.")]
    protected abstract void SetErrorResult(T context, Dictionary<string, object> customResponse);

    /// <summary>
    /// This consumes the ValidationErrorResult property in the CustomValidatorRequestContext and sets
    /// it appropriately in the response object for the token and grant validators.
    /// </summary>
    /// <param name="context">The current grant or token context</param>
    /// <param name="requestContext">The modified request context containing material used to build the response object</param>
    protected abstract void SetValidationErrorResult(T context, CustomValidatorRequestContext requestContext);
    protected abstract Task SetSuccessResult(T context, User user, List<Claim> claims,
        Dictionary<string, object> customResponse);

    protected abstract ClaimsPrincipal GetSubject(T context);

    /// <summary>
    /// Check if the user is required to authenticate via SSO. If the user requires SSO, but they are
    /// logging in using an API Key (client_credentials) then they are allowed to bypass the SSO requirement.
    /// If the GrantType is authorization_code or client_credentials we know the user is trying to login
    /// using the SSO flow so they are allowed to continue.
    /// </summary>
    /// <param name="user">user trying to login</param>
    /// <param name="grantType">magic string identifying the grant type requested</param>
    /// <returns>true if sso required; false if not required or already in process</returns>
    private async Task<bool> RequireSsoLoginAsync(User user, string grantType)
    {
        if (grantType == "authorization_code" || grantType == "client_credentials")
        {
            // Already using SSO to authenticate, or logging-in via api key to skip SSO requirement
            // allow to authenticate successfully
            return false;
        }

        // Check if user belongs to any organization with an active SSO policy
        var anySsoPoliciesApplicableToUser = await PolicyService.AnyPoliciesApplicableToUserAsync(
                                                user.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        if (anySsoPoliciesApplicableToUser)
        {
            return true;
        }

        // Default - SSO is not required
        return false;
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

        if (orgs.Count == 0)
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
