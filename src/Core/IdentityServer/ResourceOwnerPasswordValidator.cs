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
using System.Linq;
using Bit.Core.Models;

namespace Bit.Core.IdentityServer
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private UserManager<User> _userManager;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDeviceService _deviceService;
        private readonly IUserService _userService;

        public ResourceOwnerPasswordValidator(
            UserManager<User> userManager,
            IDeviceRepository deviceRepository,
            IDeviceService deviceService,
            IUserService userService)
        {
            _userManager = userManager;
            _deviceRepository = deviceRepository;
            _deviceService = deviceService;
            _userService = userService;
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var twoFactorToken = context.Request.Raw["TwoFactorToken"]?.ToString();
            var twoFactorProvider = context.Request.Raw["TwoFactorProvider"]?.ToString();
            var twoFactorRemember = context.Request.Raw["TwoFactorRemember"]?.ToString() == "1";
            var twoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) && !string.IsNullOrWhiteSpace(twoFactorProvider);
            var credentialsCorrect = false;

            if(!string.IsNullOrWhiteSpace(context.UserName))
            {
                var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
                if(user != null)
                {
                    credentialsCorrect = await _userManager.CheckPasswordAsync(user, context.Password);
                    if(credentialsCorrect)
                    {
                        TwoFactorProviderType twoFactorProviderType = TwoFactorProviderType.Authenticator; // Just defaulting it
                        if(!twoFactorRequest && await TwoFactorRequiredAsync(user))
                        {
                            await BuildTwoFactorResultAsync(user, context);
                            return;
                        }

                        if(twoFactorRequest && !Enum.TryParse(twoFactorProvider, out twoFactorProviderType))
                        {
                            await BuildTwoFactorResultAsync(user, context);
                            return;
                        }

                        if(!twoFactorRequest || await VerifyTwoFactor(user, twoFactorProviderType, twoFactorToken))
                        {
                            var device = await SaveDeviceAsync(user, context);
                            await BuildSuccessResultAsync(user, context, device, twoFactorRequest,
                                twoFactorProviderType, twoFactorRemember);
                            return;
                        }

                        if(twoFactorRequest && twoFactorProviderType == TwoFactorProviderType.Remember)
                        {
                            await Task.Delay(2000); // Delay for brute force.
                            await BuildTwoFactorResultAsync(user, context);
                            return;
                        }
                    }
                }
            }

            await Task.Delay(2000); // Delay for brute force.
            BuildErrorResult(credentialsCorrect && twoFactorRequest, context);
        }

        private async Task BuildSuccessResultAsync(User user, ResourceOwnerPasswordValidationContext context, Device device,
            bool twoFactorRequest, TwoFactorProviderType twoFactorProviderType, bool twoFactorRemember)
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

            if(twoFactorRequest && twoFactorRemember)
            {
                var token = await _userManager.GenerateTwoFactorTokenAsync(user, TwoFactorProviderType.Remember.ToString());
                customResponse.Add("TwoFactorToken", token);
            }

            context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
                identityProvider: "bitwarden",
                claims: claims.Count > 0 ? claims : null,
                customResponse: customResponse);
        }

        private async Task BuildTwoFactorResultAsync(User user, ResourceOwnerPasswordValidationContext context)
        {
            var providerKeys = new List<byte>();
            var providers = new Dictionary<byte, Dictionary<string, object>>();
            var enabledProviders = user.GetTwoFactorProviders()?.Where(p => user.TwoFactorProviderIsEnabled(p.Key));
            if(enabledProviders == null)
            {
                BuildErrorResult(false, context);
                return;
            }

            foreach(var provider in enabledProviders)
            {
                providerKeys.Add((byte)provider.Key);
                var infoDict = await BuildTwoFactorParams(user, provider.Key, provider.Value);
                providers.Add((byte)provider.Key, infoDict);
            }

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
                new Dictionary<string, object>
                {
                    { "TwoFactorProviders", providers.Keys },
                    { "TwoFactorProviders2", providers }
                });

            if(enabledProviders.Count() == 1 && enabledProviders.First().Key == TwoFactorProviderType.Email)
            {
                // Send email now if this is their only 2FA method
                await _userService.SendTwoFactorEmailAsync(user);
            }
        }

        private void BuildErrorResult(bool twoFactorRequest, ResourceOwnerPasswordValidationContext context)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant,
                customResponse: new Dictionary<string, object>
                {{
                    "ErrorModel", new ErrorResponseModel(twoFactorRequest ?
                        "Two-step token is invalid. Try again." : "Username or password is incorrect. Try again.")
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

        private async Task<bool> VerifyTwoFactor(User user, TwoFactorProviderType type, string token)
        {
            if(!user.TwoFactorProviderIsEnabled(type))
            {
                return false;
            }

            switch(type)
            {
                case TwoFactorProviderType.Authenticator:
                case TwoFactorProviderType.Duo:
                case TwoFactorProviderType.YubiKey:
                case TwoFactorProviderType.U2f:
                case TwoFactorProviderType.Remember:
                    return await _userManager.VerifyTwoFactorTokenAsync(user, type.ToString(), token);
                case TwoFactorProviderType.Email:
                    return await _userService.VerifyTwoFactorEmailAsync(user, token);
                default:
                    return false;
            }
        }

        private async Task<Dictionary<string, object>> BuildTwoFactorParams(User user, TwoFactorProviderType type,
            TwoFactorProvider provider)
        {
            if(!user.TwoFactorProviderIsEnabled(type))
            {
                return null;
            }

            switch(type)
            {
                case TwoFactorProviderType.Duo:
                case TwoFactorProviderType.U2f:
                case TwoFactorProviderType.Email:
                case TwoFactorProviderType.YubiKey:
                    var token = await _userManager.GenerateTwoFactorTokenAsync(user, type.ToString());
                    if(type == TwoFactorProviderType.Duo)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Host"] = provider.MetaData["Host"],
                            ["Signature"] = token
                        };
                    }
                    else if(type == TwoFactorProviderType.U2f)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Challenges"] = token
                        };
                    }
                    else if(type == TwoFactorProviderType.Email)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Email"] = RedactEmail((string)provider.MetaData["Email"])
                        };
                    }
                    else if(type == TwoFactorProviderType.YubiKey)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Nfc"] = (bool)provider.MetaData["Nfc"]
                        };
                    }
                    return null;
                default:
                    return null;
            }
        }

        private static string RedactEmail(string email)
        {
            var emailParts = email.Split('@');

            string shownPart = null;
            if(emailParts[0].Length > 2 && emailParts[0].Length <= 4)
            {
                shownPart = emailParts[0].Substring(0, 1);
            }
            else if(emailParts[0].Length > 4)
            {
                shownPart = emailParts[0].Substring(0, 2);
            }
            else
            {
                shownPart = string.Empty;
            }

            string redactedPart = null;
            if(emailParts[0].Length > 4)
            {
                redactedPart = new string('*', emailParts[0].Length - 2);
            }
            else
            {
                redactedPart = new string('*', emailParts[0].Length - shownPart.Length);
            }

            return $"{shownPart}{redactedPart}@{emailParts[1]}";
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
