using System.Security.Claims;
using System.Text.Json;
using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Fido2NetLib;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class WebAuthnGrantValidator : BaseRequestValidator<ExtensionGrantValidationContext>, IExtensionGrantValidator
{
    public const string GrantType = "webauthn";

    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;
    private readonly IAssertWebAuthnLoginCredentialCommand _assertWebAuthnLoginCredentialCommand;
    private readonly IDeviceValidator _deviceValidator;

    public WebAuthnGrantValidator(
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
        IAssertWebAuthnLoginCredentialCommand assertWebAuthnLoginCredentialCommand
        )
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
        _assertionOptionsDataProtector = assertionOptionsDataProtector;
        _assertWebAuthnLoginCredentialCommand = assertWebAuthnLoginCredentialCommand;
        _deviceValidator = deviceValidator;
    }

    string IExtensionGrantValidator.GrantType => "webauthn";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var rawToken = context.Request.Raw.Get("token");
        var rawDeviceResponse = context.Request.Raw.Get("deviceResponse");
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(rawDeviceResponse))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var verified = _assertionOptionsDataProtector.TryUnprotect(rawToken, out var token) &&
            token.TokenIsValid(WebAuthnLoginAssertionOptionsScope.Authentication);
        var deviceResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(rawDeviceResponse);

        if (!verified)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest);
            return;
        }

        var (user, credential) = await _assertWebAuthnLoginCredentialCommand.AssertWebAuthnLoginCredential(token.Options, deviceResponse);
        var validatorContext = new CustomValidatorRequestContext
        {
            User = user,
            KnownDevice = await _deviceValidator.KnownDeviceAsync(user, context.Request)
        };

        UserDecryptionOptionsBuilder.WithWebAuthnLoginCredential(credential);

        await ValidateAsync(context, context.Request, validatorContext);
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

    protected override Task SetSuccessResult(ExtensionGrantValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
            identityProvider: Constants.IdentityProvider,
            claims: claims.Count > 0 ? claims : null,
            customResponse: customResponse);
        return Task.CompletedTask;
    }

    protected override ClaimsPrincipal GetSubject(ExtensionGrantValidationContext context)
    {
        return context.Result.Subject;
    }

    protected override void SetTwoFactorResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
            customResponse);
    }

    protected override void SetSsoResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Sso authentication required.",
            customResponse);
    }

    protected override void SetErrorResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse: customResponse);
    }
}
