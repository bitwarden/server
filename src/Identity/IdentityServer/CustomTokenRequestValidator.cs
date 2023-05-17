using System.Security.Claims;
using Bit.Core;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.IdentityServer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;

#nullable enable

namespace Bit.Identity.IdentityServer;

public class CustomTokenRequestValidator : BaseRequestValidator<CustomTokenRequestValidationContext>,
    ICustomTokenRequestValidator
{
    private readonly UserManager<User> _userManager;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IFeatureService _featureService;

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
        ILogger<CustomTokenRequestValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository,
        IUserRepository userRepository,
        IPolicyService policyService,
        IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> tokenDataFactory,
        IFeatureService featureService)
        : base(userManager, deviceRepository, deviceService, userService, eventService,
            organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
            applicationCacheService, mailService, logger, currentContext, globalSettings, policyRepository,
            userRepository, policyService, tokenDataFactory)
    {
        _userManager = userManager;
        _ssoConfigRepository = ssoConfigRepository;
        _featureService = featureService;
    }

    public async Task ValidateAsync(CustomTokenRequestValidationContext context)
    {
        string[] allowedGrantTypes = { "authorization_code", "client_credentials" };
        if (!allowedGrantTypes.Contains(context.Result.ValidatedRequest.GrantType)
            || context.Result.ValidatedRequest.ClientId.StartsWith("organization")
            || context.Result.ValidatedRequest.ClientId.StartsWith("installation")
            || context.Result.ValidatedRequest.ClientId.StartsWith("internal")
            || context.Result.ValidatedRequest.Client.AllowedScopes.Contains(ApiScopes.ApiSecrets))
        {
            if (context.Result.ValidatedRequest.Client.Properties.TryGetValue("encryptedPayload", out var payload) &&
                !string.IsNullOrWhiteSpace(payload))
            {
                context.Result.CustomResponse = new Dictionary<string, object> { { "encrypted_payload", payload } };
            }

            return;
        }

        await ValidateAsync(context, context.Result.ValidatedRequest,
            new CustomValidatorRequestContext { KnownDevice = true });
    }

    protected async override Task<bool> ValidateContextAsync(CustomTokenRequestValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        var email = context.Result.ValidatedRequest.Subject?.GetDisplayName()
                    ?? context.Result.ValidatedRequest.ClientClaims
                        ?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.Email)?.Value;
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

        // Attempts to find ssoConfigData for a given validate request subject
        // this is actually guarenteed to pretty often be null, because more than just sso login requests will come
        // through here
        var ssoConfigData = await GetSsoConfigurationDataAsync(context.Result.ValidatedRequest.Subject);

        // You can't put this below the user.MasterPassword != null check because TDE users can still have a MasterPassword
        // It's worth noting that CurrentContext here will build a user in LaunchDarkly that is anonymous but DOES belong
        // to an organization. So we will not be able to turn this feature on for only a single user, only for an entire 
        // organization at a time.
        if (ssoConfigData != null && _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, CurrentContext))
        {
            context.Result.CustomResponse["MemberDecryptionOptions"] = CreateMemberDecryptionOptions(ssoConfigData, user).ToList();
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
        // This does a double check, that ssoConfigData is not null and that it has the KeyConnector member decryption type
        if (ssoConfigData is { MemberDecryptionType: MemberDecryptionType.KeyConnector } && !string.IsNullOrEmpty(ssoConfigData.KeyConnectorUrl))
        {
            // TODO: Can be removed in the future
            context.Result.CustomResponse["KeyConnectorUrl"] = ssoConfigData.KeyConnectorUrl;
            // Prevent clients redirecting to set-password
            context.Result.CustomResponse["ResetMasterPassword"] = false;
        }
    }

    private async Task<SsoConfigurationData?> GetSsoConfigurationDataAsync(ClaimsPrincipal? subject)
    {
        var organizationClaim = subject?.FindFirstValue("organizationId");

        if (organizationClaim == null || !Guid.TryParse(organizationClaim, out var organizationId))
        {
            return null;
        }

        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organizationId);
        if (ssoConfig == null)
        {
            return null;
        }

        return ssoConfig.GetData();
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

    /// <summary>
    /// 
    /// </summary>
    private static IEnumerable<UserDecryptionOption> CreateMemberDecryptionOptions(SsoConfigurationData ssoConfigurationData, User user)
    {
        if (ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.KeyConnector } && !string.IsNullOrEmpty(ssoConfigurationData.KeyConnectorUrl))
        {
            // KeyConnector makes it mutually exclusive
            yield return new KeyConnectorUserDecryptionOption(ssoConfigurationData.KeyConnectorUrl);
            yield break;
        }

        if (ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.TrustedDeviceEncryption })
        {
            // TrustedDeviceEncryption only exists for SSO, but if that ever changes this value won't always be true
            yield return new TrustedDeviceUserDecryptionOption(hasAdminApproval: true);
        }

        if (!string.IsNullOrEmpty(user.MasterPassword))
        {
            yield return new MasterPasswordUserDecryptionOption();
        }
    }
}
