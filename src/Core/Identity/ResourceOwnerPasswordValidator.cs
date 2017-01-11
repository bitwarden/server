using Bit.Core.Domains;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Core.Identity
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly UserManager<User> _userManager;

        public ResourceOwnerPasswordValidator(
            UserManager<User> userManager)
        {
            _userManager = userManager;
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
                            new Claim("authmethod", "Application"),
                            new Claim("nameid", user.Id.ToString()),
                            new Claim("email", user.Email.ToString()),
                            new Claim("securitystamp", user.SecurityStamp)
                        });
                    return;
                }
            }

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Username or password is incorrect.");
        }
    }
}
