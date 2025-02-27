﻿using System.Diagnostics;
using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.IdentityServer;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;
using HandlebarsDotNet;
using Microsoft.AspNetCore.Identity;

#nullable enable

namespace Bit.Identity.IdentityServer.RequestValidators;

public class CustomTokenRequestValidator : BaseRequestValidator<CustomTokenRequestValidationContext>,
    ICustomTokenRequestValidator
{
    private readonly UserManager<User> _userManager;
    private readonly IUpdateInstallationCommand _updateInstallationCommand;

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
        IUserDecryptionOptionsBuilder userDecryptionOptionsBuilder,
        IUpdateInstallationCommand updateInstallationCommand
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
        _updateInstallationCommand = updateInstallationCommand;
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
        string clientId = context.Result.ValidatedRequest.ClientId;
        if (!allowedGrantTypes.Contains(context.Result.ValidatedRequest.GrantType)
            || clientId.StartsWith("organization")
            || clientId.StartsWith("installation")
            || clientId.StartsWith("internal")
            || context.Result.ValidatedRequest.Client.AllowedScopes.Contains(ApiScopes.ApiSecrets))
        {
            if (context.Result.ValidatedRequest.Client.Properties.TryGetValue("encryptedPayload", out var payload) &&
                !string.IsNullOrWhiteSpace(payload))
            {
                context.Result.CustomResponse = new Dictionary<string, object> { { "encrypted_payload", payload } };

            }
            if (FeatureService.IsEnabled(FeatureFlagKeys.RecordInstallationLastActivityDate)
                && context.Result.ValidatedRequest.ClientId.StartsWith("installation"))
            {
                var installationIdPart = clientId.Split(".")[1];
                await RecordActivityForInstallation(clientId.Split(".")[1]);
            }
            return;
        }
        await ValidateAsync(context, context.Result.ValidatedRequest, new CustomValidatorRequestContext { });
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

    [Obsolete("Consider using SetGrantValidationErrorResult instead.")]
    protected override void SetTwoFactorResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = "invalid_grant";
        context.Result.ErrorDescription = "Two factor required.";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    [Obsolete("Consider using SetGrantValidationErrorResult instead.")]
    protected override void SetSsoResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = "invalid_grant";
        context.Result.ErrorDescription = "Sso authentication required.";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    [Obsolete("Consider using SetGrantValidationErrorResult instead.")]
    protected override void SetErrorResult(CustomTokenRequestValidationContext context,
        Dictionary<string, object> customResponse)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = "invalid_grant";
        context.Result.IsError = true;
        context.Result.CustomResponse = customResponse;
    }

    protected override void SetValidationErrorResult(
        CustomTokenRequestValidationContext context, CustomValidatorRequestContext requestContext)
    {
        Debug.Assert(context.Result is not null);
        context.Result.Error = requestContext.ValidationErrorResult.Error;
        context.Result.IsError = requestContext.ValidationErrorResult.IsError;
        context.Result.ErrorDescription = requestContext.ValidationErrorResult.ErrorDescription;
        context.Result.CustomResponse = requestContext.CustomResponse;
    }

    /// <summary>
    /// To help mentally separate organizations that self host from abandoned
    /// organizations we hook in to the token refresh event for installations
    /// to write a simple `DateTime.Now` to the database.
    /// </summary>
    /// <remarks>
    /// This works well because installations don't phone home very often.
    /// Currently self hosted installations only refresh tokens every 24
    /// hours or so for the sake of hooking in to cloud's push relay service.
    /// If installations ever start refreshing tokens more frequently we may need to
    /// adjust this to avoid making a bunch of unnecessary database calls!
    /// </remarks>
    private async Task RecordActivityForInstallation(string? installationIdString)
    {
        if (!Guid.TryParse(installationIdString, out var installationId))
        {
            return;
        }
        await _updateInstallationCommand.UpdateLastActivityDateAsync(installationId);
    }
}
