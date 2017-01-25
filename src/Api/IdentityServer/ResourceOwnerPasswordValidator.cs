using Bit.Api.Models.Response;
using Bit.Core.Domains;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Api.IdentityServer
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private UserManager<User> _userManager;
        private IdentityOptions _identityOptions;
        private JwtBearerOptions _jwtBearerOptions;
        private JwtBearerIdentityOptions _jwtBearerIdentityOptions;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ResourceOwnerPasswordValidator(
            IDeviceRepository deviceRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _deviceRepository = deviceRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            Init();

            var oldAuthBearer = context.Request.Raw["OldAuthBearer"]?.ToString();
            var twoFactorToken = context.Request.Raw["TwoFactorToken"]?.ToString();
            var twoFactorProvider = context.Request.Raw["TwoFactorProvider"]?.ToString();
            var twoFactorRequest = !string.IsNullOrWhiteSpace(twoFactorToken) && !string.IsNullOrWhiteSpace(twoFactorProvider);

            if(!string.IsNullOrWhiteSpace(oldAuthBearer) && _jwtBearerOptions != null)
            {
                // support transferring the old auth bearer token
                var ticket = ValidateOldAuthBearer(oldAuthBearer);
                if(ticket != null && ticket.Principal != null)
                {
                    var idClaim = ticket.Principal.Claims
                        .FirstOrDefault(c => c.Type == _identityOptions.ClaimsIdentity.UserIdClaimType);
                    var securityTokenClaim = ticket.Principal.Claims
                        .FirstOrDefault(c => c.Type == _identityOptions.ClaimsIdentity.SecurityStampClaimType);
                    if(idClaim != null && securityTokenClaim != null)
                    {
                        var user = await _userManager.FindByIdAsync(idClaim.Value);
                        if(user != null && user.SecurityStamp == securityTokenClaim.Value)
                        {
                            var device = await SaveDeviceAsync(user, context);
                            BuildSuccessResult(user, context, null);
                            return;
                        }
                    }
                }
            }
            else if(!string.IsNullOrWhiteSpace(context.UserName))
            {
                var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
                if(user != null)
                {
                    if(await _userManager.CheckPasswordAsync(user, context.Password))
                    {
                        if(!twoFactorRequest && await TwoFactorRequiredAsync(user))
                        {
                            BuildTwoFactorResult(user, context);
                            return;
                        }

                        if(!twoFactorRequest || await _userManager.VerifyTwoFactorTokenAsync(user, twoFactorProvider, twoFactorToken))
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

        private void Init()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            _userManager = httpContext.RequestServices.GetRequiredService<UserManager<User>>();
            _identityOptions = httpContext.RequestServices.GetRequiredService<IOptions<IdentityOptions>>()?.Value ?? new IdentityOptions();
            _jwtBearerIdentityOptions = httpContext.RequestServices.GetRequiredService<IOptions<JwtBearerIdentityOptions>>()?.Value;
            _jwtBearerOptions = Core.Identity.JwtBearerAppBuilderExtensions.BuildJwtBearerOptions(_jwtBearerIdentityOptions);
        }

        private void BuildSuccessResult(User user, ResourceOwnerPasswordValidationContext context, Device device)
        {
            var claims = new List<Claim>
            {
                // Deprecated claims for backwards compatability
                new Claim(ClaimTypes.AuthenticationMethod, "Application"),
                new Claim(_identityOptions.ClaimsIdentity.UserIdClaimType, user.Id.ToString())
            };

            if(device != null)
            {
                claims.Add(new Claim("device", device.Identifier));
            }

            context.Result = new GrantValidationResult(user.Id.ToString(), "Application", identityProvider: "bitwarden",
                claims: claims.Count > 0 ? claims : null);
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
                    { "TwoFactorRequired", true },
                    { "TwoFactorProviders", providers }
                });
        }

        private void BuildErrorResult(bool twoFactorRequest, ResourceOwnerPasswordValidationContext context)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse:
                new Dictionary<string, object>
                {{
                    "ErrorModel", new ErrorResponseModel(twoFactorRequest ?
                        "Code is not correct. Try again." : "Username or password is incorrect. Try again.")
                }});
        }

        private AuthenticationTicket ValidateOldAuthBearer(string token)
        {
            SecurityToken validatedToken;
            foreach(var validator in _jwtBearerOptions.SecurityTokenValidators)
            {
                if(validator.CanReadToken(token))
                {
                    ClaimsPrincipal principal;
                    try
                    {
                        principal = validator.ValidateToken(token,
                            _jwtBearerOptions.TokenValidationParameters, out validatedToken);
                    }
                    catch
                    {
                        continue;
                    }

                    var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(),
                        _jwtBearerOptions.AuthenticationScheme);

                    return ticket;
                }
            }

            return null;
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

        private async Task<Device> SaveDeviceAsync(User user, ResourceOwnerPasswordValidationContext context)
        {
            var device = GetDeviceFromRequest(context);
            if(device != null)
            {
                var existingDevice = await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
                if(existingDevice == null)
                {
                    device.UserId = user.Id;
                    await _deviceRepository.CreateAsync(device);
                    return device;
                }
            }

            return null;
        }
    }
}
