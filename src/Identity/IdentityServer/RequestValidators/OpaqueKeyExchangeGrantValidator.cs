using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class OpaqueKeyExchangeGrantValidator : BaseRequestValidator<ExtensionGrantValidationContext>, IExtensionGrantValidator
{
    public const string GrantType = "opaque-ke";
    private readonly IOpaqueKeyExchangeService _opaqueKeyExchangeService;
    private readonly IFeatureService _featureService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<OpaqueKeyExchangeGrantValidator> _logger;

    public OpaqueKeyExchangeGrantValidator(
        UserManager<User> userManager,
        IUserService userService,
        IEventService eventService,
        IDeviceValidator deviceValidator,
        ITwoFactorAuthenticationValidator twoFactorAuthenticationValidator,
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        ILogger<OpaqueKeyExchangeGrantValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IUserRepository userRepository,
        IPolicyService policyService,
        IFeatureService featureService,
        ISsoConfigRepository ssoConfigRepository,
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder,
        IOpaqueKeyExchangeService opaqueKeyExchangeService)
        : base(
            userManager,
            userService,
            eventService,
            deviceValidator,
            twoFactorAuthenticationValidator,
            organizationUserRepository,
            mailService,
            logger,
            currentContext,
            globalSettings,
            userRepository,
            policyService,
            featureService,
            ssoConfigRepository,
            userDecryptionOptionsBuilder)
    {
        _opaqueKeyExchangeService = opaqueKeyExchangeService;
        _currentContext = currentContext;
        _featureService = featureService;
        _logger = logger;
    }

    string IExtensionGrantValidator.GrantType => "opaque-ke";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.OpaqueKeyExchange))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var sessionId = context.Request.Raw.Get("sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var user = await _opaqueKeyExchangeService.GetUserForAuthenticatedSession(Guid.Parse(sessionId));
        if (user == null || !AuthEmailHeaderIsValid(user))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant,
                "Auth-Email header invalid.");
            return;
        }

        await ValidateAsync(context, context.Request, new CustomValidatorRequestContext { User = user });
    }

    protected override Task<bool> ValidateContextAsync(ExtensionGrantValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        if (validatorContext.User == null)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    protected override async Task SetSuccessResult(ExtensionGrantValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
            identityProvider: Constants.IdentityProvider,
            claims: claims.Count > 0 ? claims : null,
            customResponse: customResponse);
        await _opaqueKeyExchangeService.ClearAuthenticationSession(Guid.Parse(context.Request.Raw.Get("sessionId")));
    }

    protected override ClaimsPrincipal GetSubject(ExtensionGrantValidationContext context)
    {
        return context.Result.Subject;
    }

    [Obsolete("Consider using SetValidationErrorResult instead.")]
    protected override void SetTwoFactorResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
            customResponse);
    }

    [Obsolete("Consider using SetValidationErrorResult instead.")]
    protected override void SetSsoResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Sso authentication required.",
            customResponse);
    }

    [Obsolete("Consider using SetValidationErrorResult instead.")]
    protected override void SetErrorResult(ExtensionGrantValidationContext context, Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse: customResponse);
    }

    protected override void SetValidationErrorResult(
        ExtensionGrantValidationContext context, CustomValidatorRequestContext requestContext)
    {
        context.Result = new GrantValidationResult
        {
            Error = requestContext.ValidationErrorResult.Error,
            ErrorDescription = requestContext.ValidationErrorResult.ErrorDescription,
            IsError = true,
            CustomResponse = requestContext.CustomResponse
        };
    }

    /// <summary>
    /// This method matches the Email in the header to the email fetched from the SessionId
    /// </summary>
    /// <param name="user">User associated with the Authenticated cache</param>
    /// <returns>true if the emails match false otherwise</returns>
    private bool AuthEmailHeaderIsValid(User user)
    {
        if (_currentContext.HttpContext.Request.Headers.TryGetValue("Auth-Email", out var authEmailHeader))
        {
            try
            {
                var authEmailDecoded = CoreHelpers.Base64UrlDecodeString(authEmailHeader);
                if (authEmailDecoded != user.Email)
                {
                    return false;
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is FormatException)
            {
                _logger.LogError(e, "Invalid B64 encoding for Auth-Email header {UserId}", user.Id);
                return false;
            }
        }
        else
        {
            return false;
        }
        return true;
    }
}
