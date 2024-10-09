using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Identity.Test.Wrappers;

public class BaseRequestValidationContextFake
{
    public ValidatedTokenRequest ValidatedTokenRequest;
    public CustomValidatorRequestContext CustomValidatorRequestContext;
    public GrantValidationResult GrantResult;

    public BaseRequestValidationContextFake(
        ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext customValidatorRequestContext,
        GrantValidationResult grantResult)
    {
        ValidatedTokenRequest = tokenRequest;
        CustomValidatorRequestContext = customValidatorRequestContext;
        GrantResult = grantResult;
    }
}

interface IBaseRequestValidatorTestWrapper
{
    Task ValidateAsync(BaseRequestValidationContextFake context);
}

public class BaseRequestValidatorTestWrapper : BaseRequestValidator<BaseRequestValidationContextFake>,
IBaseRequestValidatorTestWrapper
{

    /*
    * Some of the logic trees call `ValidateContextAsync`. Since this is a test wrapper, we set the return value
    * of ValidateContextAsync() to whatever we need for the specific test case.
    */
    public bool isValid { get; set; }
    public BaseRequestValidatorTestWrapper(
        UserManager<User> userManager,
        IDeviceRepository deviceRepository,
        IDeviceService deviceService,
        IUserService userService,
        IEventService eventService,
        IOrganizationDuoUniversalTokenProvider organizationDuoWebTokenProvider,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService,
        IMailService mailService,
        ILogger logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IUserRepository userRepository,
        IPolicyService policyService,
        IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> tokenDataFactory,
        IFeatureService featureService,
        ISsoConfigRepository ssoConfigRepository,
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder) :
        base(
            userManager,
            deviceRepository,
            deviceService,
            userService,
            eventService,
            organizationDuoWebTokenProvider,
            organizationRepository,
            organizationUserRepository,
            applicationCacheService,
            mailService,
            logger,
            currentContext,
            globalSettings,
            userRepository,
            policyService,
            tokenDataFactory,
            featureService,
            ssoConfigRepository,
            userDecryptionOptionsBuilder)
    {
    }

    public async Task ValidateAsync(
        BaseRequestValidationContextFake context)
    {
        await ValidateAsync(context, context.ValidatedTokenRequest, context.CustomValidatorRequestContext);
    }

    public async Task<Tuple<bool, Organization>> TestRequiresTwoFactorAsync(
        User user,
        ValidatedTokenRequest context)
    {
        return await RequiresTwoFactorAsync(user, context);
    }

    protected override ClaimsPrincipal GetSubject(
        BaseRequestValidationContextFake context)
    {
        return context.ValidatedTokenRequest.Subject ?? new ClaimsPrincipal();
    }

    protected override void SetErrorResult(
        BaseRequestValidationContextFake context,
        Dictionary<string, object> customResponse)
    {
        context.GrantResult = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse: customResponse);
    }

    protected override void SetSsoResult(
        BaseRequestValidationContextFake context,
        Dictionary<string, object> customResponse)
    {
        context.GrantResult = new GrantValidationResult(
            TokenRequestErrors.InvalidGrant, "Sso authentication required.", customResponse);
    }

    protected override Task SetSuccessResult(
        BaseRequestValidationContextFake context,
        User user,
        List<Claim> claims,
        Dictionary<string, object> customResponse)
    {
        context.GrantResult = new GrantValidationResult(customResponse: customResponse);
        return Task.CompletedTask;
    }

    protected override void SetTwoFactorResult(
        BaseRequestValidationContextFake context,
        Dictionary<string, object> customResponse)
    { }

    protected override Task<bool> ValidateContextAsync(
        BaseRequestValidationContextFake context,
        CustomValidatorRequestContext validatorContext)
    {
        return Task.FromResult(isValid);
    }
}
