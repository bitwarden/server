using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Core.Services;
using System.Linq;
using Bit.Core.Models;
using Bit.Core.Identity;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Bit.Core.Models.Api;

namespace Bit.Core.IdentityServer
{
    public abstract class BaseRequestValidator<T> where T : class
    {
        private UserManager<User> _userManager;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDeviceService _deviceService;
        private readonly IUserService _userService;
        private readonly IEventService _eventService;
        private readonly IOrganizationDuoWebTokenProvider _organizationDuoWebTokenProvider;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly IMailService _mailService;
        private readonly ILogger<ResourceOwnerPasswordValidator> _logger;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;
        private readonly IPolicyRepository _policyRepository;

        public BaseRequestValidator(
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
            CurrentContext currentContext,
            GlobalSettings globalSettings,
            IPolicyRepository policyRepository)
        {
            _userManager = userManager;
            _deviceRepository = deviceRepository;
            _deviceService = deviceService;
            _userService = userService;
            _eventService = eventService;
            _organizationDuoWebTokenProvider = organizationDuoWebTokenProvider;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _applicationCacheService = applicationCacheService;
            _mailService = mailService;
            _logger = logger;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
            _policyRepository = policyRepository;
        }

        protected async Task ValidateAsync(T context, ValidatedTokenRequest request)
        {
            var twoFactorToken = request.Raw["TwoFactorToken"]?.ToString();
            var twoFactorProvider = request.Raw["TwoFactorProvider"]?.ToString();
            var twoFactorRemember = request.Raw["TwoFactorRemember"]?.ToString() == "1";
            var twoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) &&
                !string.IsNullOrWhiteSpace(twoFactorProvider);

            var (user, valid) = await ValidateContextAsync(context);
            if (!valid)
            {
                await BuildErrorResultAsync("Username or password is incorrect. Try again.", false, context, user);
                return;
            }

            var twoFactorRequirement = await RequiresTwoFactorAsync(user);
            if (twoFactorRequirement.Item1)
            {
                // Just defaulting it
                var twoFactorProviderType = TwoFactorProviderType.Authenticator;
                if (!twoFactorRequest || !Enum.TryParse(twoFactorProvider, out twoFactorProviderType))
                {
                    await BuildTwoFactorResultAsync(user, twoFactorRequirement.Item2, context);
                    return;
                }

                var verified = await VerifyTwoFactor(user, twoFactorRequirement.Item2,
                    twoFactorProviderType, twoFactorToken);
                if (!verified && twoFactorProviderType != TwoFactorProviderType.Remember)
                {
                    await BuildErrorResultAsync("Two-step token is invalid. Try again.", true, context, user);
                    return;
                }
                else if (!verified && twoFactorProviderType == TwoFactorProviderType.Remember)
                {
                    // Delay for brute force.
                    await Task.Delay(2000);
                    await BuildTwoFactorResultAsync(user, twoFactorRequirement.Item2, context);
                    return;
                }
            }
            else
            {
                twoFactorRequest = false;
                twoFactorRemember = false;
                twoFactorToken = null;
            }

            // Returns true if can finish validation process
            if (await IsValidAuthTypeAsync(user, request.GrantType))
            {
                var device = await SaveDeviceAsync(user, request);
                if (device == null)
                {
                    await BuildErrorResultAsync("No device information provided.", false, context, user);
                }
                await BuildSuccessResultAsync(user, context, device, twoFactorRequest && twoFactorRemember);
            }
            else
            {
                SetSsoResult(context, new Dictionary<string, object>
                {{
                    "ErrorModel", new ErrorResponseModel("SSO authentication is required.")
                }});
            }
        }

        protected abstract Task<(User, bool)> ValidateContextAsync(T context);

        protected async Task BuildSuccessResultAsync(User user, T context, Device device, bool sendRememberToken)
        {
            await _eventService.LogUserEventAsync(user.Id, EventType.User_LoggedIn);

            var claims = new List<Claim>();

            if (device != null)
            {
                claims.Add(new Claim("device", device.Identifier));
            }

            var customResponse = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(user.PrivateKey))
            {
                customResponse.Add("PrivateKey", user.PrivateKey);
            }

            if (!string.IsNullOrWhiteSpace(user.Key))
            {
                customResponse.Add("Key", user.Key);
            }

            customResponse.Add("ResetMasterPassword", string.IsNullOrWhiteSpace(user.MasterPassword));
            customResponse.Add("Kdf", (byte)user.Kdf);
            customResponse.Add("KdfIterations", user.KdfIterations);

            if (sendRememberToken)
            {
                var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.Remember));
                customResponse.Add("TwoFactorToken", token);
            }

            SetSuccessResult(context, user, claims, customResponse);
        }

        protected async Task BuildTwoFactorResultAsync(User user, Organization organization, T context)
        {
            var providerKeys = new List<byte>();
            var providers = new Dictionary<string, Dictionary<string, object>>();

            var enabledProviders = new List<KeyValuePair<TwoFactorProviderType, TwoFactorProvider>>();
            if (organization?.GetTwoFactorProviders() != null)
            {
                enabledProviders.AddRange(organization.GetTwoFactorProviders().Where(
                    p => organization.TwoFactorProviderIsEnabled(p.Key)));
            }

            if (user.GetTwoFactorProviders() != null)
            {
                foreach (var p in user.GetTwoFactorProviders())
                {
                    if (await _userService.TwoFactorProviderIsEnabledAsync(p.Key, user))
                    {
                        enabledProviders.Add(p);
                    }
                }
            }

            if (!enabledProviders.Any())
            {
                await BuildErrorResultAsync("No two-step providers enabled.", false, context, user);
                return;
            }

            foreach (var provider in enabledProviders)
            {
                providerKeys.Add((byte)provider.Key);
                var infoDict = await BuildTwoFactorParams(organization, user, provider.Key, provider.Value);
                providers.Add(((byte)provider.Key).ToString(), infoDict);
            }

            SetTwoFactorResult(context,
                new Dictionary<string, object>
                {
                    { "TwoFactorProviders", providers.Keys },
                    { "TwoFactorProviders2", providers }
                });

            if (enabledProviders.Count() == 1 && enabledProviders.First().Key == TwoFactorProviderType.Email)
            {
                // Send email now if this is their only 2FA method
                await _userService.SendTwoFactorEmailAsync(user);
            }
        }

        protected async Task BuildErrorResultAsync(string message, bool twoFactorRequest, T context, User user)
        {
            if (user != null)
            {
                await _eventService.LogUserEventAsync(user.Id,
                    twoFactorRequest ? EventType.User_FailedLogIn2fa : EventType.User_FailedLogIn);
            }

            if (_globalSettings.SelfHosted)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId,
                    string.Format("Failed login attempt{0}{1}", twoFactorRequest ? ", 2FA invalid." : ".",
                        $" {_currentContext.IpAddress}"));
            }

            await Task.Delay(2000); // Delay for brute force.
            SetErrorResult(context,
                new Dictionary<string, object>
                {{
                    "ErrorModel", new ErrorResponseModel(message)
                }});
        }

        protected abstract void SetTwoFactorResult(T context, Dictionary<string, object> customResponse);

        protected abstract void SetSsoResult(T context, Dictionary<string, object> customResponse);

        protected abstract void SetSuccessResult(T context, User user, List<Claim> claims,
            Dictionary<string, object> customResponse);

        protected abstract void SetErrorResult(T context, Dictionary<string, object> customResponse);

        private async Task<Tuple<bool, Organization>> RequiresTwoFactorAsync(User user)
        {
            var individualRequired = _userManager.SupportsUserTwoFactor &&
                await _userManager.GetTwoFactorEnabledAsync(user) &&
                (await _userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;

            Organization firstEnabledOrg = null;
            var orgs = (await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id))
                .ToList();
            if (orgs.Any())
            {
                var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
                var twoFactorOrgs = orgs.Where(o => OrgUsing2fa(orgAbilities, o.Id));
                if (twoFactorOrgs.Any())
                {
                    var userOrgs = await _organizationRepository.GetManyByUserIdAsync(user.Id);
                    firstEnabledOrg = userOrgs.FirstOrDefault(
                        o => orgs.Any(om => om.Id == o.Id) && o.TwoFactorIsEnabled());
                }
            }

            return new Tuple<bool, Organization>(individualRequired || firstEnabledOrg != null, firstEnabledOrg);
        }

        private async Task<bool> IsValidAuthTypeAsync(User user, string grantType)
        {
            if (grantType == "authorization_code")
            {
                // Already using SSO to authorize, finish successfully
                return true;
            }

            // Is user apart of any orgs? Use cache for initial checks.
            var orgs = (await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, user.Id))
                .ToList();
            if (orgs.Any())
            {
                // Get all org abilities
                var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
                // Parse all user orgs that are enabled and have the ability to use sso
                var ssoOrgs = orgs.Where(o => OrgCanUseSso(orgAbilities, o.Id));
                if (ssoOrgs.Any())
                {
                    // Parse users orgs and determine if require sso policy is enabled
                    var userOrgs = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id);
                    foreach (var userOrg in userOrgs.Where(o => o.Enabled && o.UseSso))
                    {
                        var orgPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(userOrg.OrganizationId,
                            PolicyType.RequireSso);
                        // Owners and Admins are exempt from this policy
                        if (orgPolicy != null && orgPolicy.Enabled && 
                            userOrg.Type != OrganizationUserType.Owner && userOrg.Type != OrganizationUserType.Admin)
                        {
                            return false;
                        }
                    }
                }
            }

            // Default - continue validation process
            return true;
        }

        private bool OrgUsing2fa(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
        {
            return orgAbilities != null && orgAbilities.ContainsKey(orgId) &&
                orgAbilities[orgId].Enabled && orgAbilities[orgId].Using2fa;
        }

        private bool OrgCanUseSso(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
        {
            return orgAbilities != null && orgAbilities.ContainsKey(orgId) &&
                   orgAbilities[orgId].Enabled && orgAbilities[orgId].UseSso;
        }

        private Device GetDeviceFromRequest(ValidatedRequest request)
        {
            var deviceIdentifier = request.Raw["DeviceIdentifier"]?.ToString();
            var deviceType = request.Raw["DeviceType"]?.ToString();
            var deviceName = request.Raw["DeviceName"]?.ToString();
            var devicePushToken = request.Raw["DevicePushToken"]?.ToString();

            if (string.IsNullOrWhiteSpace(deviceIdentifier) || string.IsNullOrWhiteSpace(deviceType) ||
                string.IsNullOrWhiteSpace(deviceName) || !Enum.TryParse(deviceType, out DeviceType type))
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

        private async Task<bool> VerifyTwoFactor(User user, Organization organization, TwoFactorProviderType type,
            string token)
        {
            switch (type)
            {
                case TwoFactorProviderType.Authenticator:
                case TwoFactorProviderType.Email:
                case TwoFactorProviderType.Duo:
                case TwoFactorProviderType.YubiKey:
                case TwoFactorProviderType.U2f:
                case TwoFactorProviderType.Remember:
                    if (type != TwoFactorProviderType.Remember &&
                        !(await _userService.TwoFactorProviderIsEnabledAsync(type, user)))
                    {
                        return false;
                    }
                    return await _userManager.VerifyTwoFactorTokenAsync(user,
                        CoreHelpers.CustomProviderName(type), token);
                case TwoFactorProviderType.OrganizationDuo:
                    if (!organization?.TwoFactorProviderIsEnabled(type) ?? true)
                    {
                        return false;
                    }

                    return await _organizationDuoWebTokenProvider.ValidateAsync(token, organization, user);
                default:
                    return false;
            }
        }

        private async Task<Dictionary<string, object>> BuildTwoFactorParams(Organization organization, User user,
            TwoFactorProviderType type, TwoFactorProvider provider)
        {
            switch (type)
            {
                case TwoFactorProviderType.Duo:
                case TwoFactorProviderType.U2f:
                case TwoFactorProviderType.Email:
                case TwoFactorProviderType.YubiKey:
                    if (!(await _userService.TwoFactorProviderIsEnabledAsync(type, user)))
                    {
                        return null;
                    }

                    var token = await _userManager.GenerateTwoFactorTokenAsync(user,
                        CoreHelpers.CustomProviderName(type));
                    if (type == TwoFactorProviderType.Duo)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Host"] = provider.MetaData["Host"],
                            ["Signature"] = token
                        };
                    }
                    else if (type == TwoFactorProviderType.U2f)
                    {
                        // TODO: Remove "Challenges" in a future update. Deprecated.
                        var tokens = token?.Split('|');
                        return new Dictionary<string, object>
                        {
                            ["Challenge"] = tokens != null && tokens.Length > 0 ? tokens[0] : null,
                            ["Challenges"] = tokens != null && tokens.Length > 1 ? tokens[1] : null
                        };
                    }
                    else if (type == TwoFactorProviderType.Email)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Email"] = token
                        };
                    }
                    else if (type == TwoFactorProviderType.YubiKey)
                    {
                        return new Dictionary<string, object>
                        {
                            ["Nfc"] = (bool)provider.MetaData["Nfc"]
                        };
                    }
                    return null;
                case TwoFactorProviderType.OrganizationDuo:
                    if (await _organizationDuoWebTokenProvider.CanGenerateTwoFactorTokenAsync(organization))
                    {
                        return new Dictionary<string, object>
                        {
                            ["Host"] = provider.MetaData["Host"],
                            ["Signature"] = await _organizationDuoWebTokenProvider.GenerateAsync(organization, user)
                        };
                    }
                    return null;
                default:
                    return null;
            }
        }

        private async Task<Device> SaveDeviceAsync(User user, ValidatedTokenRequest request)
        {
            var device = GetDeviceFromRequest(request);
            if (device != null)
            {
                var existingDevice = await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
                if (existingDevice == null)
                {
                    device.UserId = user.Id;
                    await _deviceService.SaveAsync(device);

                    var now = DateTime.UtcNow;
                    if (now - user.CreationDate > TimeSpan.FromMinutes(10))
                    {
                        var deviceType = device.Type.GetType().GetMember(device.Type.ToString())
                            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName();
                        if (!_globalSettings.DisableEmailNewDevice)
                        {
                            await _mailService.SendNewDeviceLoggedInEmail(user.Email, deviceType, now,
                                _currentContext.IpAddress);
                        }
                    }

                    return device;
                }

                return existingDevice;
            }

            return null;
        }
    }
}
