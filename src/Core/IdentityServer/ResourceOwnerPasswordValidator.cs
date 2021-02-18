using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Core.Services;
using Bit.Core.Identity;
using Bit.Core.Context;
using Microsoft.Extensions.Logging;

namespace Bit.Core.IdentityServer
{
    public class ResourceOwnerPasswordValidator : BaseRequestValidator<ResourceOwnerPasswordValidationContext>,
        IResourceOwnerPasswordValidator
    {
        private UserManager<User> _userManager;
        private readonly IUserService _userService;

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
            IPolicyRepository policyRepository)
            : base(userManager, deviceRepository, deviceService, userService, eventService,
                  organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
                  applicationCacheService, mailService, logger, currentContext, globalSettings, policyRepository)
        {
            _userManager = userManager;
            _userService = userService;
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            await ValidateAsync(context, context.Request);
        }

        protected async override Task<(User, bool)> ValidateContextAsync(ResourceOwnerPasswordValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(context.UserName))
            {
                return (null, false);
            }

            var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
            if (user == null || !await _userService.CheckPasswordAsync(user, context.Password))
            {
                return (user, false);
            }

            return (user, true);
        }

        protected override void SetSuccessResult(ResourceOwnerPasswordValidationContext context, User user,
            List<Claim> claims, Dictionary<string, object> customResponse)
        {
            context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
                identityProvider: "bitwarden",
                claims: claims.Count > 0 ? claims : null,
                customResponse: customResponse);
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
    }
}
