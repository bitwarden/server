using System.Diagnostics;
using System.Security.Claims;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.IdentityServer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;
using HandlebarsDotNet;
using IdentityModel;
using Microsoft.AspNetCore.Identity;

#nullable enable

namespace Bit.Identity.IdentityServer.RequestValidators;

public class CustomTokenRequestValidator : BaseRequestValidator<CustomTokenRequestValidationContext>,
    ICustomTokenRequestValidator
{
    private readonly UserManager<User> _userManager;

    public CustomTokenRequestValidator(
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
        IUserRepository userRepository,
        IPolicyService policyService,
        IFeatureService featureService,
        ISsoConfigRepository ssoConfigRepository,
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder
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
        _userManager = userManager;
    }

    public async Task ValidateAsync(CustomTokenRequestValidationContext context)
    {
        Debug.Assert(context.Result is not null);
        if (context.Result.ValidatedRequest.GrantType == "refresh_token")
        {
            // Force legacy users to the web for migration
            if (await _userService.IsLegacyUser(GetSubject(context)?.GetSubjectId()) &&
                context.Result.ValidatedRequest.ClientId != "web")
            {
                await FailAuthForLegacyUserAsync(null, context);
                return;
            }
        }

        string[] allowedGrantTypes = ["authorization_code", "client_credentials"];
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
        Debug.Assert(context.Result is not null);
        var email = context.Result.ValidatedRequest.Subject?.GetDisplayName()
                    ?? context.Result.ValidatedRequest.ClientClaims
                        ?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.Email)?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            validatorContext.User = await _userManager.FindByEmailAsync(email);
        }
        return validatorContext.User != null;
    }

    protected override Task SetSuccessResult(CustomTokenRequestValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
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
            return Task.CompletedTask;
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
            return Task.CompletedTask;
        }

        // Key connector data should have already been set in the decryption options
        // for backwards compatibility we set them this way too. We can eventually get rid of this
        // when all clients don't read them from the existing locations.
        if (!context.Result.CustomResponse.TryGetValue("UserDecryptionOptions", out var userDecryptionOptionsObj) ||
            userDecryptionOptionsObj is not UserDecryptionOptions userDecryptionOptions)
        {
            return Task.CompletedTask;
        }
        if (userDecryptionOptions is { KeyConnectorOption: { } })
        {
            context.Result.CustomResponse["KeyConnectorUrl"] = userDecryptionOptions.KeyConnectorOption.KeyConnectorUrl;
            context.Result.CustomResponse["ResetMasterPassword"] = false;
        }
        return Task.CompletedTask;
    }

    protected override ClaimsPrincipal? GetSubject(CustomTokenRequestValidationContext context)
    {
        Debug.Assert(context.Result is not null);
        return context.Result.ValidatedRequest.Subject;
    }

    protected override void SetTwoFactorResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = "invalid_grant";
        context.Result.ErrorDescription = "Two factor required.";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    protected override void SetSsoResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = "invalid_grant";
        context.Result.ErrorDescription = "Single Sign on required.";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    protected override void SetErrorResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = "invalid_grant";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }
}
