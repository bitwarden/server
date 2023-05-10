using System.Security.Claims;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Auth.Models.Business.Tokenables;

namespace Bit.Identity.IdentityServer;

public class ExtensionGrantValidator : BaseRequestValidator<ExtensionGrantValidationContext>, IExtensionGrantValidator
{
    private UserManager<User> _userManager;
    private readonly IDataProtectorTokenFactory<WebAuthnLoginTokenable> _webAuthnLoginTokenizer;

    public ExtensionGrantValidator(
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
        ILogger<CustomTokenRequestValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IPolicyRepository policyRepository,
        IUserRepository userRepository,
        IPolicyService policyService,
        IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> tokenDataFactory,
        IDataProtectorTokenFactory<WebAuthnLoginTokenable> webAuthnLoginTokenizer)
        : base(userManager, deviceRepository, deviceService, userService, eventService,
              organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
              applicationCacheService, mailService, logger, currentContext, globalSettings, policyRepository,
              userRepository, policyService, tokenDataFactory)
    {
        _userManager = userManager;
        _webAuthnLoginTokenizer = webAuthnLoginTokenizer;
    }

    public string GrantType => "extension";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var email = context.Request.Raw.Get("email");
        var token = context.Request.Raw.Get("token");
        var type = context.Request.Raw.Get("type");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(type))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var user = await _userManager.FindByEmailAsync(email.ToLowerInvariant());
        var validatorContext = new CustomValidatorRequestContext
        {
            User = user,
            KnownDevice = await KnownDeviceAsync(user, context.Request)
        };

        await ValidateAsync(context, context.Request, validatorContext);
    }

    protected override async Task<bool> ValidateContextAsync(ExtensionGrantValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        var token = context.Request.Raw.Get("token");
        var type = context.Request.Raw.Get("type");
        if (validatorContext.User == null || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(type))
        {
            return false;
        }
        var verified = _webAuthnLoginTokenizer.TryUnprotect(token, out var tokenData) &&
            tokenData.Valid && tokenData.TokenIsValid(validatorContext.User);
        return verified;
    }

    protected override Task SetSuccessResult(ExtensionGrantValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
            identityProvider: "bitwarden",
            claims: claims.Count > 0 ? claims : null,
            customResponse: customResponse);
        return Task.CompletedTask;
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
