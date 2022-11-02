using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer;

public class CustomTokenRequestValidator : BaseRequestValidator<CustomTokenRequestValidationContext>,
    ICustomTokenRequestValidator
{
    private UserManager<User> _userManager;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public CustomTokenRequestValidator(
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
        ISsoConfigRepository ssoConfigRepository,
        IUserRepository userRepository,
        ICaptchaValidationService captchaValidationService)
        : base(userManager, deviceRepository, deviceService, userService, eventService,
              organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
              applicationCacheService, mailService, logger, currentContext, globalSettings, policyRepository,
              userRepository, captchaValidationService)
    {
        _userManager = userManager;
        _ssoConfigRepository = ssoConfigRepository;
        _organizationRepository = organizationRepository;
    }

    public async Task ValidateAsync(CustomTokenRequestValidationContext context)
    {
        string[] allowedGrantTypes = { "authorization_code", "client_credentials" };
        if (!allowedGrantTypes.Contains(context.Result.ValidatedRequest.GrantType)
            || context.Result.ValidatedRequest.ClientId.StartsWith("organization")
            || context.Result.ValidatedRequest.ClientId.StartsWith("installation")
            || context.Result.ValidatedRequest.ClientId.StartsWith("internal"))
        {
            return;
        }
        await ValidateAsync(context, context.Result.ValidatedRequest,
            new CustomValidatorRequestContext { KnownDevice = true });
    }

    protected async override Task<bool> ValidateContextAsync(CustomTokenRequestValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        var email = context.Result.ValidatedRequest.Subject?.GetDisplayName()
            ?? context.Result.ValidatedRequest.ClientClaims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.Email)?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            validatorContext.User = await _userManager.FindByEmailAsync(email);
        }
        return validatorContext.User != null;
    }

    protected override async Task SetSuccessResult(CustomTokenRequestValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        context.Result.CustomResponse = customResponse;
        if (claims?.Any() ?? false)
        {
            context.Result.ValidatedRequest.Client.AlwaysSendClientClaims = true;
            context.Result.ValidatedRequest.Client.ClientClaimsPrefix = string.Empty;
            foreach (var claim in claims)
            {
                context.Result.ValidatedRequest.ClientClaims.Add(claim);
            }
        }

        if (context.Result.CustomResponse == null || user.MasterPassword != null)
        {
            return;
        }

        // KeyConnector responses below

        // Apikey login
        if (context.Result.ValidatedRequest.GrantType == "client_credentials")
        {
            if (user.UsesKeyConnector)
            {
                // KeyConnectorUrl is configured in the CLI client, we just need to tell the client to use it
                context.Result.CustomResponse["ApiUseKeyConnector"] = true;
                context.Result.CustomResponse["ResetMasterPassword"] = false;
            }
            return;
        }

        // SSO login
        var organizationClaim = context.Result.ValidatedRequest.Subject?.FindFirst(c => c.Type == "organizationId");
        if (organizationClaim?.Value != null)
        {
            var organizationId = new Guid(organizationClaim.Value);

            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organizationId);
            var ssoConfigData = ssoConfig.GetData();

            if (ssoConfigData is { KeyConnectorEnabled: true } && !string.IsNullOrEmpty(ssoConfigData.KeyConnectorUrl))
            {
                context.Result.CustomResponse["KeyConnectorUrl"] = ssoConfigData.KeyConnectorUrl;
                // Prevent clients redirecting to set-password
                context.Result.CustomResponse["ResetMasterPassword"] = false;
            }
        }
    }

    protected override void SetTwoFactorResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result.Error = "invalid_grant";
        context.Result.ErrorDescription = "Two factor required.";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    protected override void SetSsoResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result.Error = "invalid_grant";
        context.Result.ErrorDescription = "Single Sign on required.";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    protected override void SetErrorResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result.Error = "invalid_grant";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }
}
