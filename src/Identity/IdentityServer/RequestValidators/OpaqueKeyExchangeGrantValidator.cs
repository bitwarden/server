using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class OpaqueKeyExchangeGrantValidator : BaseRequestValidator<ExtensionGrantValidationContext>, IExtensionGrantValidator
{
    public const string GrantType = "opaque-ke";
    private IUserRepository userRepository;
    private IOpaqueKeyExchangeService opaqueKeyExchangeService;

    public OpaqueKeyExchangeGrantValidator(
        UserManager<User> userManager,
        IUserService userService,
        IEventService eventService,
        IDeviceValidator deviceValidator,
        ITwoFactorAuthenticationValidator twoFactorAuthenticationValidator,
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        ILogger<CustomTokenRequestValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        ISsoConfigRepository ssoConfigRepository,
        IUserRepository userRepository,
        IPolicyService policyService,
        IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> assertionOptionsDataProtector,
        IFeatureService featureService,
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
        this.userRepository = userRepository;
        this.opaqueKeyExchangeService = opaqueKeyExchangeService;
    }

    string IExtensionGrantValidator.GrantType => "opaque-ke";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var sessionId = context.Request.Raw.Get("sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var user = await opaqueKeyExchangeService.GetUserForAuthenticatedSession(Guid.Parse(sessionId));
        if (user == null)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
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
        await opaqueKeyExchangeService.ClearAuthenticatedSession(Guid.Parse(context.Request.Raw.Get("sessionId")));
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
}
