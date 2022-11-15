using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer;

public class ResourceOwnerPasswordValidator : BaseRequestValidator<ResourceOwnerPasswordValidationContext>,
    IResourceOwnerPasswordValidator
{
    private UserManager<User> _userManager;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly ICaptchaValidationService _captchaValidationService;
    private readonly IAuthRequestRepository _authRequestRepository;
    public ResourceOwnerPasswordValidator(
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
        ILogger<ResourceOwnerPasswordValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IPolicyRepository policyRepository,
        ICaptchaValidationService captchaValidationService,
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository)
        : base(userManager, deviceRepository, deviceService, userService, eventService,
              organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
              applicationCacheService, mailService, logger, currentContext, globalSettings, policyRepository,
              userRepository, captchaValidationService)
    {
        _userManager = userManager;
        _userService = userService;
        _currentContext = currentContext;
        _captchaValidationService = captchaValidationService;
        _authRequestRepository = authRequestRepository;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        if (!AuthEmailHeaderIsValid(context))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant,
                "Auth-Email header invalid.");
            return;
        }

        var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
        var validatorContext = new CustomValidatorRequestContext
        {
            User = user,
            KnownDevice = await KnownDeviceAsync(user, context.Request)
        };
        string bypassToken = null;
        if (!validatorContext.KnownDevice &&
            _captchaValidationService.RequireCaptchaValidation(_currentContext, user))
        {
            var captchaResponse = context.Request.Raw["captchaResponse"]?.ToString();

            if (string.IsNullOrWhiteSpace(captchaResponse))
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Captcha required.",
                    new Dictionary<string, object>
                    {
                        { _captchaValidationService.SiteKeyResponseKeyName, _captchaValidationService.SiteKey },
                    });
                return;
            }

            validatorContext.CaptchaResponse = await _captchaValidationService.ValidateCaptchaResponseAsync(
                captchaResponse, _currentContext.IpAddress, user);
            if (!validatorContext.CaptchaResponse.Success)
            {
                await BuildErrorResultAsync("Captcha is invalid. Please refresh and try again", false, context, null);
                return;
            }
            bypassToken = _captchaValidationService.GenerateCaptchaBypassToken(user);
        }

        await ValidateAsync(context, context.Request, validatorContext);
        if (context.Result.CustomResponse != null && bypassToken != null)
        {
            context.Result.CustomResponse["CaptchaBypassToken"] = bypassToken;
        }
    }

    protected async override Task<bool> ValidateContextAsync(ResourceOwnerPasswordValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        if (string.IsNullOrWhiteSpace(context.UserName) || validatorContext.User == null)
        {
            return false;
        }

        var authRequestId = context.Request.Raw["AuthRequest"]?.ToString()?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(authRequestId) && Guid.TryParse(authRequestId, out var authRequestGuid))
        {
            var authRequest = await _authRequestRepository.GetByIdAsync(authRequestGuid);
            if (authRequest != null)
            {
                var requestAge = DateTime.UtcNow - authRequest.CreationDate;
                if (requestAge < TimeSpan.FromHours(1) &&
                    CoreHelpers.FixedTimeEquals(authRequest.AccessCode, context.Password))
                {
                    authRequest.AuthenticationDate = DateTime.UtcNow;
                    await _authRequestRepository.ReplaceAsync(authRequest);
                    return true;
                }
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
            identityProvider: "bitwarden",
            claims: claims.Count > 0 ? claims : null,
            customResponse: customResponse);
        return Task.CompletedTask;
    }

    protected override void SetTwoFactorResult(ResourceOwnerPasswordValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
            customResponse);
    }

    protected override void SetSsoResult(ResourceOwnerPasswordValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Sso authentication required.",
            customResponse);
    }

    protected override void SetErrorResult(ResourceOwnerPasswordValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse: customResponse);
    }

    private bool AuthEmailHeaderIsValid(ResourceOwnerPasswordValidationContext context)
    {
        if (!_currentContext.HttpContext.Request.Headers.ContainsKey("Auth-Email"))
        {
            return false;
        }
        else
        {
            try
            {
                var authEmailHeader = _currentContext.HttpContext.Request.Headers["Auth-Email"];
                var authEmailDecoded = CoreHelpers.Base64UrlDecodeString(authEmailHeader);

                if (authEmailDecoded != context.UserName)
                {
                    return false;
                }
            }
            catch (System.Exception e) when (e is System.InvalidOperationException || e is System.FormatException)
            {
                // Invalid B64 encoding
                return false;
            }
        }

        return true;
    }
}
