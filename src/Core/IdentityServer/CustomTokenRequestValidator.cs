using System;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Context;
using System.Linq;
using System.Text.Json;
using Bit.Core.Identity;
using Bit.Core.Models.Data;
using Microsoft.Extensions.Logging;
using IdentityServer4.Extensions;
using IdentityModel;

namespace Bit.Core.IdentityServer
{
    public class CustomTokenRequestValidator : BaseRequestValidator<CustomTokenRequestValidationContext>,
        ICustomTokenRequestValidator
    {
        private UserManager<User> _userManager;
        private readonly ISsoConfigRepository _ssoConfigRepository;

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
            ISsoConfigRepository ssoConfigRepository)
            : base(userManager, deviceRepository, deviceService, userService, eventService,
                  organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
                  applicationCacheService, mailService, logger, currentContext, globalSettings, policyRepository)
        {
            _userManager = userManager;
            _ssoConfigRepository = ssoConfigRepository;
        }

        public async Task ValidateAsync(CustomTokenRequestValidationContext context)
        {
            string[] allowedGrantTypes = { "authorization_code", "client_credentials" };
            if (!allowedGrantTypes.Contains(context.Result.ValidatedRequest.GrantType) ||
                context.Result.ValidatedRequest.ClientId.StartsWith("organization"))
            {
                return;
            }
            await ValidateAsync(context, context.Result.ValidatedRequest);

            if (context.Result.CustomResponse != null)
            {
                var organizationClaim = context.Result.ValidatedRequest.Subject?.FindFirst(c => c.Type == "organizationId");
                var organizationId = organizationClaim?.Value ?? "";

                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(new Guid(organizationId));
                var ssoConfigData = ssoConfig.GetData();

                if (ssoConfigData is { UseCryptoAgent: true } && !string.IsNullOrEmpty(ssoConfigData.CryptoAgentUrl))
                {
                    context.Result.CustomResponse["CryptoAgentUrl"] = ssoConfigData.CryptoAgentUrl;
                    // Prevent clients redirecting to set-password
                    // TODO: Figure out if we can move this logic to the clients since this might break older clients
                    //  although we will have issues either way with some clients supporting crypto anent and some not
                    //  suggestion: We should roll out the clients before enabling it server wise
                    context.Result.CustomResponse["ResetMasterPassword"] = false;
                }
            }
        }

        protected async override Task<(User, bool)> ValidateContextAsync(CustomTokenRequestValidationContext context)
        {
            var email = context.Result.ValidatedRequest.Subject?.GetDisplayName() 
                ?? context.Result.ValidatedRequest.ClientClaims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.Email)?.Value;
            var user = string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email);
            return (user, user != null);
        }

        protected override void SetSuccessResult(CustomTokenRequestValidationContext context, User user,
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
}
