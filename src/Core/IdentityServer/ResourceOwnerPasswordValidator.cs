using Bit.Core.Models.Api;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Core.Services;

namespace Bit.Core.IdentityServer
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private UserManager<User> _userManager;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDeviceService _deviceService;

        public ResourceOwnerPasswordValidator(
            UserManager<User> userManager,
            IDeviceRepository deviceRepository,
            IDeviceService deviceService)
        {
            _userManager = userManager;
            _deviceRepository = deviceRepository;
            _deviceService = deviceService;
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var twoFactorToken = context.Request.Raw["TwoFactorToken"]?.ToString();
            var twoFactorProvider = context.Request.Raw["TwoFactorProvider"]?.ToString();
            var twoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) && !string.IsNullOrWhiteSpace(twoFactorProvider);

            if(!string.IsNullOrWhiteSpace(context.UserName))
            {
                var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
                if(user != null)
                {
                    if(await _userManager.CheckPasswordAsync(user, context.Password))
                    {
                        TwoFactorProviderType twoFactorProviderType = TwoFactorProviderType.Authenticator; // Just defaulting it
                        if(!twoFactorRequest && await TwoFactorRequiredAsync(user))
                        {
                            BuildTwoFactorResult(user, context);
                            return;
                        }

                        if(twoFactorRequest && !Enum.TryParse(twoFactorProvider, out twoFactorProviderType))
                        {
                            BuildTwoFactorResult(user, context);
                            return;
                        }

                        if(!twoFactorRequest ||
                            await _userManager.VerifyTwoFactorTokenAsync(user, twoFactorProviderType.ToString(), twoFactorToken))
                        {
                            var device = await SaveDeviceAsync(user, context);
                            BuildSuccessResult(user, context, device);
                            return;
                        }
                    }
                }
            }

            await Task.Delay(2000); // Delay for brute force.
            BuildErrorResult(twoFactorRequest, context);
        }

        private void BuildSuccessResult(User user, ResourceOwnerPasswordValidationContext context, Device device)
        {
            var claims = new List<Claim>();

            if(device != null)
            {
                claims.Add(new Claim("device", device.Identifier));
            }

            var customResponse = new Dictionary<string, object>();
            if(!string.IsNullOrWhiteSpace(user.PrivateKey))
            {
                customResponse.Add("PrivateKey", user.PrivateKey);
            }

            if(!string.IsNullOrWhiteSpace(user.Key))
            {
                customResponse.Add("Key", user.Key);
            }

            context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
                identityProvider: "bitwarden",
                claims: claims.Count > 0 ? claims : null,
                customResponse: customResponse);
        }

        private void BuildTwoFactorResult(User user, ResourceOwnerPasswordValidationContext context)
        {
            var providers = new List<byte>();
            if(user.TwoFactorProvider.HasValue)
            {
                providers.Add((byte)user.TwoFactorProvider.Value);
            }

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
                new Dictionary<string, object>
                {
                    { "TwoFactorProviders", providers }
                });
        }

        private void BuildErrorResult(bool twoFactorRequest, ResourceOwnerPasswordValidationContext context)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant,
                customResponse: new Dictionary<string, object>
                {{
                    "ErrorModel", new ErrorResponseModel(twoFactorRequest ?
                        "Code is not correct. Try again." : "Username or password is incorrect. Try again.")
                }});
        }

        private async Task<bool> TwoFactorRequiredAsync(User user)
        {
            return _userManager.SupportsUserTwoFactor &&
                await _userManager.GetTwoFactorEnabledAsync(user) &&
                (await _userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;
        }

        private Device GetDeviceFromRequest(ResourceOwnerPasswordValidationContext context)
        {
            var deviceIdentifier = context.Request.Raw["DeviceIdentifier"]?.ToString();
            var deviceType = context.Request.Raw["DeviceType"]?.ToString();
            var deviceName = context.Request.Raw["DeviceName"]?.ToString();
            var devicePushToken = context.Request.Raw["DevicePushToken"]?.ToString();

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
                PushToken = string.IsNullOrWhiteSpace(devicePushToken) ? null : devicePushToken
            };
        }

        private async Task<Device> SaveDeviceAsync(User user, ResourceOwnerPasswordValidationContext context)
        {
            var device = GetDeviceFromRequest(context);
            if(device != null)
            {
                var existingDevice = await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
                if(existingDevice == null)
                {
                    device.UserId = user.Id;
                    await _deviceService.SaveAsync(device);
                    return device;
                }

                return existingDevice;
            }

            return null;
        }
    }
}
