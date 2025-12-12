// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class ResourceOwnerPasswordValidator : BaseRequestValidator<ResourceOwnerPasswordValidationContext>,
    IResourceOwnerPasswordValidator
{
    private UserManager<User> _userManager;
    private readonly ICurrentContext _currentContext;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IDeviceValidator _deviceValidator;
    public ResourceOwnerPasswordValidator(
        UserManager<User> userManager,
        IUserService userService,
        IEventService eventService,
        IDeviceValidator deviceValidator,
        ITwoFactorAuthenticationValidator twoFactorAuthenticationValidator,
        ISsoRequestValidator ssoRequestValidator,
        IOrganizationUserRepository organizationUserRepository,
        ILogger<ResourceOwnerPasswordValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository,
        IPolicyService policyService,
        IFeatureService featureService,
        ISsoConfigRepository ssoConfigRepository,
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder,
        IPolicyRequirementQuery policyRequirementQuery,
        IMailService mailService,
        IUserAccountKeysQuery userAccountKeysQuery,
        IClientVersionValidator clientVersionValidator)
        : base(
            userManager,
            userService,
            eventService,
            deviceValidator,
            twoFactorAuthenticationValidator,
            ssoRequestValidator,
            organizationUserRepository,
            logger,
            currentContext,
            globalSettings,
            userRepository,
            policyService,
            featureService,
            ssoConfigRepository,
            userDecryptionOptionsBuilder,
            policyRequirementQuery,
            authRequestRepository,
            mailService,
            userAccountKeysQuery,
            clientVersionValidator)
    {
        _userManager = userManager;
        _currentContext = currentContext;
        _authRequestRepository = authRequestRepository;
        _deviceValidator = deviceValidator;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
        // We want to keep this device around incase the device is new for the user
        var requestDevice = DeviceValidator.GetDeviceFromRequest(context.Request);
        var knownDevice = await _deviceValidator.GetKnownDeviceAsync(user, requestDevice);
        var validatorContext = new CustomValidatorRequestContext
        {
            User = user,
            KnownDevice = knownDevice != null,
            Device = knownDevice ?? requestDevice,
        };

        await ValidateAsync(context, context.Request, validatorContext);
    }

    protected async override Task<bool> ValidateContextAsync(ResourceOwnerPasswordValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        if (string.IsNullOrWhiteSpace(context.UserName) || validatorContext.User == null)
        {
            return false;
        }

        var authRequestId = context.Request.Raw["AuthRequest"]?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(authRequestId))
        {
            // only allow valid guids
            if (!Guid.TryParse(authRequestId, out var authRequestGuid))
            {
                return false;
            }

            var authRequest = await _authRequestRepository.GetByIdAsync(authRequestGuid);

            if (authRequest == null)
            {
                return false;
            }

            // Auth request is non-null so validate it
            if (authRequest.IsValidForAuthentication(validatorContext.User.Id, context.Password))
            {
                // We save the validated auth request so that we can set it's authentication date
                // later on only upon successful authentication.
                // For example, 2FA requires a resubmission so we can't mark the auth request
                // as authenticated here.
                validatorContext.ValidatedAuthRequest = authRequest;
                return true;
            }

            return false;
        }

        if (!await _userService.CheckPasswordAsync(validatorContext.User, context.Password))
        {
            return false;
        }
        return true;
    }

    protected override Task SetSuccessResult(ResourceOwnerPasswordValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
            identityProvider: Constants.IdentityProvider,
            claims: claims.Count > 0 ? claims : null,
            customResponse: customResponse);
        return Task.CompletedTask;
    }

    [Obsolete("Consider using SetGrantValidationErrorResult instead.")]
    protected override void SetTwoFactorResult(ResourceOwnerPasswordValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
            customResponse);
    }

    [Obsolete("Consider using SetGrantValidationErrorResult instead.")]
    protected override void SetSsoResult(ResourceOwnerPasswordValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Sso authentication required.",
            customResponse);
    }

    [Obsolete("Consider using SetGrantValidationErrorResult instead.")]
    protected override void SetErrorResult(ResourceOwnerPasswordValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse: customResponse);
    }

    protected override void SetValidationErrorResult(
        ResourceOwnerPasswordValidationContext context, CustomValidatorRequestContext requestContext)
    {
        context.Result = new GrantValidationResult
        {
            Error = requestContext.ValidationErrorResult.Error,
            ErrorDescription = requestContext.ValidationErrorResult.ErrorDescription,
            IsError = true,
            CustomResponse = requestContext.CustomResponse
        };
    }

    protected override ClaimsPrincipal GetSubject(ResourceOwnerPasswordValidationContext context)
    {
        return context.Result.Subject;
    }

}
