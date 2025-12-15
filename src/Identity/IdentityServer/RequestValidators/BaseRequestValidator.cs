// FIXME: Update this file to be null safe and then delete the line below

#nullable disable

using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Queries.Interfaces;
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
    private readonly ISsoRequestValidator _ssoRequestValidator;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ILogger _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IMailService _mailService;
    private readonly IClientVersionValidator _clientVersionValidator;

    protected ICurrentContext CurrentContext { get; }
    protected IPolicyService PolicyService { get; }
    protected IFeatureService _featureService { get; }
    protected ISsoConfigRepository SsoConfigRepository { get; }
    protected IUserService _userService { get; }
    protected IUserDecryptionOptionsBuilder UserDecryptionOptionsBuilder { get; }
    protected IPolicyRequirementQuery PolicyRequirementQuery { get; }
    protected IUserAccountKeysQuery _accountKeysQuery { get; }

    public BaseRequestValidator(
        UserManager<User> userManager,
        IUserService userService,
        IEventService eventService,
        IDeviceValidator deviceValidator,
        ITwoFactorAuthenticationValidator twoFactorAuthenticationValidator,
        ISsoRequestValidator ssoRequestValidator,
        IOrganizationUserRepository organizationUserRepository,
        ILogger logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IUserRepository userRepository,
        IPolicyService policyService,
        IFeatureService featureService,
        ISsoConfigRepository ssoConfigRepository,
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder,
        IPolicyRequirementQuery policyRequirementQuery,
        IAuthRequestRepository authRequestRepository,
        IMailService mailService,
        IUserAccountKeysQuery userAccountKeysQuery,
        IClientVersionValidator clientVersionValidator
    )
    {
        _userManager = userManager;
        _userService = userService;
        _eventService = eventService;
        _deviceValidator = deviceValidator;
        _twoFactorAuthenticationValidator = twoFactorAuthenticationValidator;
        _ssoRequestValidator = ssoRequestValidator;
        _organizationUserRepository = organizationUserRepository;
        _logger = logger;
        CurrentContext = currentContext;
        _globalSettings = globalSettings;
        PolicyService = policyService;
        _userRepository = userRepository;
        _featureService = featureService;
        SsoConfigRepository = ssoConfigRepository;
        UserDecryptionOptionsBuilder = userDecryptionOptionsBuilder;
        PolicyRequirementQuery = policyRequirementQuery;
        _authRequestRepository = authRequestRepository;
        _mailService = mailService;
        _accountKeysQuery = userAccountKeysQuery;
        _clientVersionValidator = clientVersionValidator;
    }

    protected async Task ValidateAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        var validators = DetermineValidationOrder(context, request, validatorContext);
        var allValidationSchemesSuccessful = await ProcessValidatorsAsync(validators);
        if (!allValidationSchemesSuccessful)
        {
            // Each validation task is responsible for setting its own non-success status, if applicable.
            return;
        }

        await BuildSuccessResultAsync(validatorContext.User, context, validatorContext.Device,
            validatorContext.RememberMeRequested);
    }

    protected async Task FailAuthForLegacyUserAsync(User user, T context)
    {
        await BuildErrorResultAsync(
            $"Legacy encryption without a userkey is no longer supported. To recover your account, please contact support",
            false, context, user);
    }

    protected abstract Task<bool> ValidateContextAsync(T context, CustomValidatorRequestContext validatorContext);

    /// <summary>
    /// Composer for validation schemes.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="request"><see cref="Duende.IdentityServer.Validation.ValidatedTokenRequest" /></param>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>A composed array of validation scheme delegates to evaluate in order.</returns>
    private Func<Task<bool>>[] DetermineValidationOrder(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        if (RecoveryCodeRequestForSsoRequiredUserScenario())
        {
            // Support valid requests to recover 2FA (with account code) for users who require SSO
            // by organization membership.
            // This requires an evaluation of 2FA validity in front of SSO, and an opportunity for the 2FA
            // validation to perform the recovery as part of scheme validation based on the request.
            return
            [
                () => ValidateGrantSpecificContext(context, validatorContext),
                // Now check the version number of the client. Do this after ValidateContextAsync so that
                // we prevent account enumeration. If we were to do this before ValidateContextAsync, then attackers
                // could use a known invalid client version and make a request for a user (before we know if they have
                // demonstrated ownership of the account via correct credentials) and identify if they exist by getting
                // an error response back from the validator saying the user is not compatible with the client.
                () => ValidateClientVersionAsync(context, validatorContext),
                () => ValidateTwoFactorAsync(context, request, validatorContext),
                () => ValidateSsoAsync(context, request, validatorContext),
                () => ValidateNewDeviceAsync(context, request, validatorContext),
                () => ValidateLegacyMigrationAsync(context, request, validatorContext),
                () => ValidateAuthRequestAsync(validatorContext)
            ];
        }
        else
        {
            // The typical validation scenario.
            return
            [
                () => ValidateGrantSpecificContext(context, validatorContext),
                // Now check the version number of the client. Do this after ValidateContextAsync so that
                // we prevent account enumeration. If we were to do this before ValidateContextAsync, then attackers
                // could use a known invalid client version and make a request for a user (before we know if they have
                // demonstrated ownership of the account via correct credentials) and identify if they exist by getting
                // an error response back from the validator saying the user is not compatible with the client.
                () => ValidateClientVersionAsync(context, validatorContext),
                () => ValidateSsoAsync(context, request, validatorContext),
                () => ValidateTwoFactorAsync(context, request, validatorContext),
                () => ValidateNewDeviceAsync(context, request, validatorContext),
                () => ValidateLegacyMigrationAsync(context, request, validatorContext),
                () => ValidateAuthRequestAsync(validatorContext)
            ];
        }

        bool RecoveryCodeRequestForSsoRequiredUserScenario()
        {
            var twoFactorProvider = request.Raw["TwoFactorProvider"];
            var twoFactorToken = request.Raw["TwoFactorToken"];

            // Both provider and token must be present;
            // Validity of the token for a given provider will be evaluated by the TwoFactorAuthenticationValidator.
            if (string.IsNullOrWhiteSpace(twoFactorProvider) || string.IsNullOrWhiteSpace(twoFactorToken))
            {
                return false;
            }

            if (!int.TryParse(twoFactorProvider, out var providerValue))
            {
                return false;
            }

            return providerValue == (int)TwoFactorProviderType.RecoveryCode;
        }
    }

    /// <summary>
    /// Processes the validation schemes sequentially.
    /// Each validator is responsible for setting error context responses on failure and adding itself to the
    /// validatorContext's CompletedValidationSchemes (only) on success.
    /// Failure of any scheme to validate will short-circuit the collection, causing the validation error to be
    /// returned and further schemes to not be evaluated.
    /// </summary>
    /// <param name="validators">The collection of validation schemes as composed in <see cref="DetermineValidationOrder" /></param>
    /// <returns>true if all schemes validated successfully, false if any failed.</returns>
    private static async Task<bool> ProcessValidatorsAsync(params Func<Task<bool>>[] validators)
    {
        foreach (var validator in validators)
        {
            if (!await validator())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates whether the client version is compatible for the user attempting to authenticate.
    /// </summary>
    /// <returns>true if the scheme successfully passed validation, otherwise false.</returns>
    private async Task<bool> ValidateClientVersionAsync(T context, CustomValidatorRequestContext validatorContext)
    {
        var ok = _clientVersionValidator.ValidateAsync(validatorContext.User, validatorContext);
        if (ok)
        {
            return true;
        }

        SetValidationErrorResult(context, validatorContext);
        await LogFailedLoginEvent(validatorContext.User, EventType.User_FailedLogIn);
        return false;
    }

    /// <summary>
    /// Validates the user's master password, webauthen, or custom token request via the appropriate context validator.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>true if the scheme successfully passed validation, otherwise false.</returns>
    private async Task<bool> ValidateGrantSpecificContext(T context, CustomValidatorRequestContext validatorContext)
    {
        var valid = await ValidateContextAsync(context, validatorContext);
        var user = validatorContext.User;
        if (valid)
        {
            return true;
        }

        await UpdateFailedAuthDetailsAsync(user);

        await BuildErrorResultAsync("Username or password is incorrect. Try again.", false, context, user);
        return false;
    }

    /// <summary>
    /// Validates the user's organization-enforced Single Sign-on (SSO) requirement.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="request"><see cref="Duende.IdentityServer.Validation.ValidatedTokenRequest" /></param>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>true if the scheme successfully passed validation, otherwise false.</returns>
    /// <seealso cref="DetermineValidationOrder" />
    private async Task<bool> ValidateSsoAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        // TODO: Clean up Feature Flag: Remove this if block: PM-28281
        if (!_featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired))
        {
            validatorContext.SsoRequired = await RequireSsoLoginAsync(validatorContext.User, request.GrantType);
            if (!validatorContext.SsoRequired)
            {
                return true;
            }

            // Users without SSO requirement requesting 2FA recovery will be fast-forwarded through login and are
            // presented with their 2FA management area as a reminder to re-evaluate their 2FA posture after recovery and
            // review their new recovery token if desired.
            // SSO users cannot be assumed to be authenticated, and must prove authentication with their IdP after recovery.
            // As described in validation order determination, if TwoFactorRequired, the 2FA validation scheme will have been
            // evaluated, and recovery will have been performed if requested.
            // We will send a descriptive message in these cases so clients can give the appropriate feedback and redirect
            // to /login.
            if (validatorContext.TwoFactorRequired &&
                validatorContext.TwoFactorRecoveryRequested)
            {
                SetSsoResult(context,
                    new Dictionary<string, object>
                    {
                        {
                            "ErrorModel",
                            new ErrorResponseModel(
                                "Two-factor recovery has been performed. SSO authentication is required.")
                        }
                    });
                return false;
            }

            SetSsoResult(context,
                new Dictionary<string, object>
                {
                    { "ErrorModel", new ErrorResponseModel("SSO authentication is required.") }
                });
            return false;
        }
        else
        {
            var ssoValid = await _ssoRequestValidator.ValidateAsync(validatorContext.User, request, validatorContext);
            if (ssoValid)
            {
                return true;
            }

            SetValidationErrorResult(context, validatorContext);
            return ssoValid;
        }
    }

    /// <summary>
    /// Validates the user's Multi-Factor Authentication (2FA) scheme.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="request"><see cref="Duende.IdentityServer.Validation.ValidatedTokenRequest" /></param>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>true if the scheme successfully passed validation, otherwise false.</returns>
    private async Task<bool> ValidateTwoFactorAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        (validatorContext.TwoFactorRequired, var twoFactorOrganization) =
            await _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(validatorContext.User, request);

        if (!validatorContext.TwoFactorRequired)
        {
            return true;
        }

        var twoFactorToken = request.Raw["TwoFactorToken"];
        var twoFactorProvider = request.Raw["TwoFactorProvider"];
        var validTwoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) &&
                                    !string.IsNullOrWhiteSpace(twoFactorProvider);

        // 3a. Response for 2FA required and not provided state.
        if (!validTwoFactorRequest ||
            !Enum.TryParse(twoFactorProvider, out TwoFactorProviderType twoFactorProviderType))
        {
            var resultDict = await _twoFactorAuthenticationValidator
                .BuildTwoFactorResultAsync(validatorContext.User, twoFactorOrganization);
            if (resultDict == null)
            {
                await BuildErrorResultAsync("No two-step providers enabled.", false, context, validatorContext.User);
                return false;
            }

            // Include Master Password Policy in 2FA response.
            resultDict.Add("MasterPasswordPolicy", await GetMasterPasswordPolicyAsync(validatorContext.User));
            SetTwoFactorResult(context, resultDict);
            return false;
        }

        var twoFactorTokenValid =
            await _twoFactorAuthenticationValidator
                .VerifyTwoFactorAsync(validatorContext.User, twoFactorOrganization, twoFactorProviderType,
                    twoFactorToken);

        // 3b. Response for 2FA required but request is not valid or remember token expired state.
        if (!twoFactorTokenValid)
        {
            // The remember me token has expired.
            if (twoFactorProviderType == TwoFactorProviderType.Remember)
            {
                var resultDict = await _twoFactorAuthenticationValidator
                    .BuildTwoFactorResultAsync(validatorContext.User, twoFactorOrganization);

                // Include Master Password Policy in 2FA response
                resultDict.Add("MasterPasswordPolicy", await GetMasterPasswordPolicyAsync(validatorContext.User));
                SetTwoFactorResult(context, resultDict);
            }
            else
            {
                await SendFailedTwoFactorEmail(validatorContext.User, twoFactorProviderType);
                await UpdateFailedAuthDetailsAsync(validatorContext.User);
                await BuildErrorResultAsync("Two-step token is invalid. Try again.", true, context,
                    validatorContext.User);
            }

            return false;
        }

        // 3c. Given a valid token and a successful two-factor verification, if the provider type is Recovery Code,
        // recovery will have been performed as part of 2FA validation. This will be relevant for, e.g., SSO users
        // who are requesting recovery, but who will still need to log in after 2FA recovery.
        if (twoFactorProviderType == TwoFactorProviderType.RecoveryCode)
        {
            validatorContext.TwoFactorRecoveryRequested = true;
        }

        // 3d. When the 2FA authentication is successful, we can check if the user wants a
        // rememberMe token.
        var twoFactorRemember = request.Raw["TwoFactorRemember"] == "1";
        // Check if the user wants a rememberMe token.
        if (twoFactorRemember
            // if the 2FA auth was rememberMe do not send another token.
            && twoFactorProviderType != TwoFactorProviderType.Remember)
        {
            validatorContext.RememberMeRequested = true;
        }

        return true;
    }

    /// <summary>
    /// Validates whether the user is logging in from a known device.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="request"><see cref="Duende.IdentityServer.Validation.ValidatedTokenRequest" /></param>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>true if the scheme successfully passed validation, otherwise false.</returns>
    private async Task<bool> ValidateNewDeviceAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        var deviceValid = await _deviceValidator.ValidateRequestDeviceAsync(request, validatorContext);
        if (deviceValid)
        {
            return true;
        }

        SetValidationErrorResult(context, validatorContext);
        await LogFailedLoginEvent(validatorContext.User, EventType.User_FailedLogIn);
        return false;
    }

    /// <summary>
    /// Validates whether the user should be denied access on a given non-Web client and sent to the Web client
    /// for Legacy migration.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="request"><see cref="Duende.IdentityServer.Validation.ValidatedTokenRequest" /></param>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>true if the scheme successfully passed validation, otherwise false.</returns>
    private async Task<bool> ValidateLegacyMigrationAsync(T context, ValidatedTokenRequest request,
        CustomValidatorRequestContext validatorContext)
    {
        if (!UserService.IsLegacyUser(validatorContext.User) || request.ClientId == "web")
        {
            return true;
        }

        await FailAuthForLegacyUserAsync(validatorContext.User, context);
        return false;
    }

    /// <summary>
    /// Validates and updates the auth request's timestamp.
    /// </summary>
    /// <param name="validatorContext"><see cref="Bit.Identity.IdentityServer.CustomValidatorRequestContext" /></param>
    /// <returns>true on evaluation and/or completed update of the AuthRequest.</returns>
    private async Task<bool> ValidateAuthRequestAsync(CustomValidatorRequestContext validatorContext)
    {
        // TODO: PM-24324 - This should be its own validator at some point.
        if (validatorContext.ValidatedAuthRequest != null)
        {
            validatorContext.ValidatedAuthRequest.AuthenticationDate = DateTime.UtcNow;
            await _authRequestRepository.ReplaceAsync(validatorContext.ValidatedAuthRequest);
        }

        return true;
    }

    /// <summary>
    /// Responsible for building the response to the client when the user has successfully authenticated.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="context">The current request context.</param>
    /// <param name="device">The device used for authentication.</param>
    /// <param name="sendRememberToken">Whether to send a 2FA remember token.</param>
    protected async Task BuildSuccessResultAsync(User user, T context, Device device, bool sendRememberToken)
    {
        await _eventService.LogUserEventAsync(user.Id, EventType.User_LoggedIn);

        var claims = this.BuildSubjectClaims(user, context, device);

        var customResponse = await BuildCustomResponse(user, context, device, sendRememberToken);

        await ResetFailedAuthDetailsAsync(user);

        // Once we've built the claims and custom response, we can set the success result.
        // We delegate this to the derived classes, as the implementation varies based on the grant type.
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
                "Failed login attempt. Is2FARequest: {Is2FARequest} IpAddress: {IpAddress}", twoFactorRequest,
                CurrentContext.IpAddress);
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
                    formattedMessage = string.Format("Failed login attempt, 2FA invalid.{0}",
                        $" {CurrentContext.IpAddress}");
                    break;
                default:
                    formattedMessage = "Failed login attempt.";
                    break;
            }

            _logger.LogWarning(Constants.BypassFiltersEventId, "{FailedLoginMessage}", formattedMessage);
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
    [Obsolete(
        "This method is deprecated and will be removed in future versions, PM-28281. Please use the SsoRequestValidator scheme instead.")]
    private async Task<bool> RequireSsoLoginAsync(User user, string grantType)
    {
        if (grantType == "authorization_code" || grantType == "client_credentials")
        {
            // Already using SSO to authenticate, or logging-in via api key to skip SSO requirement
            // allow to authenticate successfully
            return false;
        }

        // Check if user belongs to any organization with an active SSO policy
        var ssoRequired = _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements)
            ? (await PolicyRequirementQuery.GetAsync<RequireSsoPolicyRequirement>(user.Id))
            .SsoRequired
            : await PolicyService.AnyPoliciesApplicableToUserAsync(
                user.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        if (ssoRequired)
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

    private async Task UpdateFailedAuthDetailsAsync(User user)
    {
        if (user == null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        user.FailedLoginCount = ++user.FailedLoginCount;
        user.LastFailedLoginDate = user.RevisionDate = utcNow;
        await _userRepository.ReplaceAsync(user);
    }

    private async Task SendFailedTwoFactorEmail(User user, TwoFactorProviderType failedAttemptType)
    {
        await _mailService.SendFailedTwoFactorAttemptEmailAsync(user.Email, failedAttemptType, DateTime.UtcNow,
            CurrentContext.IpAddress);
    }

    private async Task<MasterPasswordPolicyResponseModel> GetMasterPasswordPolicyAsync(User user)
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

    /// <summary>
    /// Builds the claims that will be stored on the persisted grant.
    /// These claims are supplemented by the claims in the ProfileService when the access token is returned to the client.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="context">The current request context.</param>
    /// <param name="device">The device used for authentication.</param>
    private List<Claim> BuildSubjectClaims(User user, T context, Device device)
    {
        // We are adding the security stamp claim to the list of claims that will be stored in the persisted grant.
        // We need this because we check for changes in the stamp to determine if we need to invalidate token refresh requests,
        // in the `ProfileService.IsActiveAsync` method.
        // If we don't store the security stamp in the persisted grant, we won't have the previous value to compare against.
        var claims = new List<Claim> { new Claim(Claims.SecurityStamp, user.SecurityStamp) };

        if (device != null)
        {
            claims.Add(new Claim(Claims.Device, device.Identifier));
            claims.Add(new Claim(Claims.DeviceType, device.Type.ToString()));
        }

        return claims;
    }

    /// <summary>
    /// Builds the custom response that will be sent to the client upon successful authentication, which
    /// includes the information needed for the client to initialize the user's account in state.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="context">The current request context.</param>
    /// <param name="device">The device used for authentication.</param>
    /// <param name="sendRememberToken">Whether to send a 2FA remember token.</param>
    private async Task<Dictionary<string, object>> BuildCustomResponse(User user, T context, Device device,
        bool sendRememberToken)
    {
        var customResponse = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(user.PrivateKey))
        {
            customResponse.Add("PrivateKey", user.PrivateKey);
            var accountKeys = await _accountKeysQuery.Run(user);
            customResponse.Add("AccountKeys", new PrivateKeysResponseModel(accountKeys));
        }

        if (!string.IsNullOrWhiteSpace(user.Key))
        {
            customResponse.Add("Key", user.Key);
        }

        customResponse.Add("MasterPasswordPolicy", await GetMasterPasswordPolicyAsync(user));
        customResponse.Add("ForcePasswordReset", user.ForcePasswordReset);
        customResponse.Add("ResetMasterPassword", string.IsNullOrWhiteSpace(user.MasterPassword));
        customResponse.Add("Kdf", (byte)user.Kdf);
        customResponse.Add("KdfIterations", user.KdfIterations);
        customResponse.Add("KdfMemory", user.KdfMemory);
        customResponse.Add("KdfParallelism", user.KdfParallelism);
        customResponse.Add("UserDecryptionOptions",
            await CreateUserDecryptionOptionsAsync(user, device, GetSubject(context)));

        if (sendRememberToken)
        {
            var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Remember));
            customResponse.Add("TwoFactorToken", token);
        }

        return customResponse;
    }

#nullable enable
    /// <summary>
    /// Used to create a list of all possible ways the newly authenticated user can decrypt their vault contents
    /// </summary>
    private async Task<UserDecryptionOptions> CreateUserDecryptionOptionsAsync(User user, Device device,
        ClaimsPrincipal subject)
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
