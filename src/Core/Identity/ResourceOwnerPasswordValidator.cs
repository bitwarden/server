using Bit.Core.Domains;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Core.Identity
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly UserManager<User> _userManager;
        private readonly IdentityOptions _identityOptions;

        public ResourceOwnerPasswordValidator(
            UserManager<User> userManager,
            IOptions<IdentityOptions> optionsAccessor)
        {
            _userManager = userManager;
            _identityOptions = optionsAccessor?.Value ?? new IdentityOptions();
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var user = await _userManager.FindByEmailAsync(context.UserName.ToLowerInvariant());
            if(user != null)
            {
                if(await _userManager.CheckPasswordAsync(user, context.Password))
                {
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

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Username or password is incorrect.");
        }
    }
}
