using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bit.Core.Domains;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using Bit.Core.Repositories;

namespace Bit.Core.Identity
{
    public class JwtBearerSignInManager
    {
        private readonly IDeviceRepository _deviceRepository;

        public JwtBearerSignInManager(
            UserManager<User> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<User> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            IOptions<JwtBearerIdentityOptions> jwtIdentityOptionsAccessor,
            IOptions<JwtBearerOptions> jwtOptionsAccessor,
            ILogger<JwtBearerSignInManager> logger,
            IDeviceRepository deviceRepository)
        {
            UserManager = userManager;
            Context = contextAccessor.HttpContext;
            ClaimsFactory = claimsFactory;
            IdentityOptions = optionsAccessor?.Value ?? new IdentityOptions();
            JwtIdentityOptions = jwtIdentityOptionsAccessor?.Value ?? new JwtBearerIdentityOptions();
            JwtBearerOptions = jwtOptionsAccessor?.Value ?? new JwtBearerOptions();
            _deviceRepository = deviceRepository;
        }

        internal UserManager<User> UserManager { get; set; }
        internal HttpContext Context { get; set; }
        internal IUserClaimsPrincipalFactory<User> ClaimsFactory { get; set; }
        internal IdentityOptions IdentityOptions { get; set; }
        internal JwtBearerIdentityOptions JwtIdentityOptions { get; set; }
        internal JwtBearerOptions JwtBearerOptions { get; set; }

        public async Task<ClaimsPrincipal> CreateUserPrincipalAsync(User user) => await ClaimsFactory.CreateAsync(user);

        public Task<bool> ValidateSecurityStampAsync(User user, ClaimsPrincipal principal)
        {
            if(user != null && UserManager.SupportsUserSecurityStamp)
            {
                var securityStamp = principal.FindFirstValue(IdentityOptions.ClaimsIdentity.SecurityStampClaimType);
                if(securityStamp == user.SecurityStamp)
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        public async Task<JwtBearerSignInResult> PasswordSignInAsync(User user, string password, Device device = null)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(await UserManager.CheckPasswordAsync(user, password))
            {
                var result = await SignInOrTwoFactorAsync(user);
                if(result.Succeeded && device != null)
                {
                    var existingDevice = await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
                    if(existingDevice == null)
                    {
                        device.UserId = user.Id;
                        await _deviceRepository.CreateAsync(device);
                    }
                }

                return result;
            }

            return JwtBearerSignInResult.Failed;
        }

        public async Task<JwtBearerSignInResult> PasswordSignInAsync(string userName, string password, Device device = null)
        {
            var user = await UserManager.FindByNameAsync(userName);
            if(user == null)
            {
                return JwtBearerSignInResult.Failed;
            }

            return await PasswordSignInAsync(user, password, device);
        }

        public async Task<JwtBearerSignInResult> TwoFactorSignInAsync(User user, string provider, string code)
        {
            if(user == null)
            {
                return JwtBearerSignInResult.Failed;
            }

            if(await UserManager.VerifyTwoFactorTokenAsync(user, provider, code))
            {
                var token = await SignInAsync(user, false);

                var success = JwtBearerSignInResult.Success;
                success.Token = token;
                success.User = user;

                return success;
            }

            return JwtBearerSignInResult.Failed;
        }

        private async Task<string> SignInAsync(User user, bool twoFactor)
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

            DateTime? tokenExpiration = null;
            var userPrincipal = await CreateUserPrincipalAsync(user);
            if(twoFactor)
            {
                userPrincipal.Identities.First().AddClaim(new Claim(ClaimTypes.AuthenticationMethod, JwtIdentityOptions.TwoFactorAuthenticationMethod));
                if(JwtIdentityOptions.TwoFactorTokenLifetime.HasValue)
                {
                    tokenExpiration = DateTime.UtcNow.Add(JwtIdentityOptions.TwoFactorTokenLifetime.Value);
                }
            }
            else
            {
                userPrincipal.Identities.First().AddClaim(new Claim(ClaimTypes.AuthenticationMethod, JwtIdentityOptions.AuthenticationMethod));
                if(JwtIdentityOptions.TokenLifetime.HasValue)
                {
                    tokenExpiration = DateTime.UtcNow.Add(JwtIdentityOptions.TokenLifetime.Value);
                }
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = JwtIdentityOptions.Issuer,
                SigningCredentials = JwtIdentityOptions.SigningCredentials,
                Audience = JwtIdentityOptions.Audience,
                Subject = userPrincipal.Identities.First(),
                Expires = tokenExpiration
            };

            var securityToken = handler.CreateToken(descriptor);

            return handler.WriteToken(securityToken);
        }

        private async Task<JwtBearerSignInResult> SignInOrTwoFactorAsync(User user)
        {
            if(UserManager.SupportsUserTwoFactor &&
                await UserManager.GetTwoFactorEnabledAsync(user) &&
                (await UserManager.GetValidTwoFactorProvidersAsync(user)).Count > 0)
            {
                var twoFactorToken = await SignInAsync(user, true);

                var twoFactorResult = JwtBearerSignInResult.TwoFactorRequired;
                twoFactorResult.Token = twoFactorToken;
                twoFactorResult.User = user;

                return twoFactorResult;
            }

            var token = await SignInAsync(user, false);

            var result = JwtBearerSignInResult.Success;
            result.Token = token;
            result.User = user;

            return result;
        }
    }
}
