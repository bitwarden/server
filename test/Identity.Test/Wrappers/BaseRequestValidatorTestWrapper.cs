using System.Security.Claims;
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
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute.Routing.Handlers;
using Xunit.Sdk;

namespace Bit.Identity.Test.Wrappers;

public class BaseRequestValidationContextFake
{
    public ValidatedTokenRequest ValidatedTokenRequest;
    public CustomValidatorRequestContext CustomValidatorRequestContext;

    public BaseRequestValidationContextFake(ValidatedTokenRequest tokenRequest, CustomValidatorRequestContext customValidatorRequestContext)
    {
        ValidatedTokenRequest = tokenRequest;
        CustomValidatorRequestContext = customValidatorRequestContext;
    }
}

interface IBaseRequestValidatorTestWrapper
{
    Task ValidateAsync(BaseRequestValidationContextFake context);
}

public class BaseRequestValidatorTestWrapper : BaseRequestValidator<BaseRequestValidationContextFake>,
IBaseRequestValidatorTestWrapper
{
    public BaseRequestValidatorTestWrapper(
        UserManager<User> userManager,
        IDeviceRepository deviceRepository,
        IDeviceService deviceService,
        IUserService userService,
        IEventService eventService,
        IOrganizationDuoWebTokenProvider organizationDuoWebTokenProvider,
        ITemporaryDuoWebV4SDKService duoWebV4SDKService,
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
            duoWebV4SDKService,
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
        BaseRequestValidationContextFake context) =>
        await ValidateAsync(context, context.ValidatedTokenRequest, context.CustomValidatorRequestContext);

    #region Fight me
    // override
    public void SetTestTwoFactorResult(
        BaseRequestValidationContextFake context, Dictionary<string, object> customResponse) =>
        SetTwoFactorResult(context, customResponse);
    // override
    public void SetTestSsoResult(
        BaseRequestValidationContextFake context, Dictionary<string, object> customResponse) =>
        SetSsoResult(context, customResponse);
    // override
    public void SetTestSuccessResult(
        BaseRequestValidationContextFake context, User user, List<Claim> claims, Dictionary<string, object> customResponse) =>
        SetSuccessResult(context, user, claims, customResponse);
    // override
    public void SetTestErrorResult(
        BaseRequestValidationContextFake context, Dictionary<string, object> customResponse) =>
        SetErrorResult(context, customResponse);
    // override
    public ClaimsPrincipal GetTestSubject(
        BaseRequestValidationContextFake context) => GetSubject(context);
    // override
    public async Task<bool> ValidateTestContextAsync(
        BaseRequestValidationContextFake context, CustomValidatorRequestContext validatorContext)
    {
        return await Task.FromResult(true);
    }

    // Junk?!?
    protected override ClaimsPrincipal GetSubject(
        BaseRequestValidationContextFake context) => throw new NotImplementedException();
    protected override void SetErrorResult(
        BaseRequestValidationContextFake context, Dictionary<string, object> customResponse) => throw new NotImplementedException();
    protected override void SetSsoResult(
        BaseRequestValidationContextFake context, Dictionary<string, object> customResponse) => throw new NotImplementedException();
    protected override Task SetSuccessResult(
        BaseRequestValidationContextFake context, User user, List<Claim> claims, Dictionary<string, object> customResponse) => throw new NotImplementedException();
    protected override void SetTwoFactorResult(
        BaseRequestValidationContextFake context, Dictionary<string, object> customResponse) => throw new NotImplementedException();
    protected override Task<bool> ValidateContextAsync(
        BaseRequestValidationContextFake context, CustomValidatorRequestContext validatorContext) => Task.FromResult(true);
    #endregion
}
