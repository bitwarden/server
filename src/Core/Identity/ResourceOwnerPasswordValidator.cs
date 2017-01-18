using Bit.Core.Domains;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Core.Identity
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly UserManager<User> _userManager;
        private readonly IdentityOptions _identityOptions;
        private readonly IDeviceRepository _deviceRepository;

        public ResourceOwnerPasswordValidator(
            UserManager<User> userManager,
            IOptions<IdentityOptions> optionsAccessor,
            IDeviceRepository deviceRepository)
        {
            _userManager = userManager;
            _identityOptions = optionsAccessor?.Value ?? new IdentityOptions();
            _deviceRepository = deviceRepository;
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var twoFactorCode = context.Request.Raw["twoFactorCode"]?.ToString();
            var twoFactorProvider = context.Request.Raw["twoFactorProvider"]?.ToString();
            var twoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorCode) &&
                !string.IsNullOrWhiteSpace(twoFactorProvider);

            var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
            if(user != null)
            {
                if(await _userManager.CheckPasswordAsync(user, context.Password))
                {
                    if(!twoFactorRequest && await TwoFactorRequiredAsync(user))
                    {
                        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor code required.",
                            // TODO: return something better?
                            new System.Collections.Generic.Dictionary<string, object> { { "TwoFactorRequired", true } });
                        return;
                    }

                    if(!twoFactorRequest || await _userManager.VerifyTwoFactorTokenAsync(user, twoFactorProvider, twoFactorCode))
                    {
                        await SaveDeviceAsync(user, context);

                        context.Result = new GrantValidationResult(user.Id.ToString(), "Application", identityProvider: "bitwarden",
                            claims: new Claim[] {
                                // Deprecated claims for backwards compatability
                                new Claim(ClaimTypes.AuthenticationMethod, "Application"),
                                new Claim(_identityOptions.ClaimsIdentity.UserIdClaimType, user.Id.ToString()),
                                new Claim(_identityOptions.ClaimsIdentity.UserNameClaimType, user.Email.ToString()),
                                new Claim(_identityOptions.ClaimsIdentity.SecurityStampClaimType, user.SecurityStamp)
                            });
                        return;
                    }
                }
            }

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant,
               twoFactorRequest ? "Code is not correct. Try again." : "Username or password is incorrect. Try again.");
        }

        private async Task<bool> TwoFactorRequiredAsync(User user)
        {
            return _userManager.SupportsUserTwoFactor &&
                await _userManager.GetTwoFactorEnabledAsync(user) &&
                (await _userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;
        }

        private Device GetDeviceFromRequest(ResourceOwnerPasswordValidationContext context)
        {
            var deviceIdentifier = context.Request.Raw["deviceIdentifier"]?.ToString();
            var deviceType = context.Request.Raw["deviceType"]?.ToString();
            var deviceName = context.Request.Raw["deviceName"]?.ToString();
            var devicePushToken = context.Request.Raw["devicePushToken"]?.ToString();

            DeviceType type;
            if(string.IsNullOrWhiteSpace(deviceIdentifier) || string.IsNullOrWhiteSpace(deviceType) ||
                string.IsNullOrWhiteSpace(deviceName) || !Enum.TryParse(deviceType, out type))
            {
                return null;
            }

            return new Device
            {
                Identifier = deviceIdentifier,
                Name = deviceName,
                Type = type,
                PushToken = devicePushToken
            };
        }

        private async Task SaveDeviceAsync(User user, ResourceOwnerPasswordValidationContext context)
        {
            var device = GetDeviceFromRequest(context);
            if(device != null)
            {
                var existingDevice = await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
                if(existingDevice == null)
                {
                    device.UserId = user.Id;
                    await _deviceRepository.CreateAsync(device);
                }
            }
        }
    }
}
