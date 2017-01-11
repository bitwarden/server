using Bit.Core.Domains;
using Bit.Core.Repositories;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Core.Identity
{
    public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly IUserRepository _userRepository;
        private readonly UserManager<User> _userManager;

        public ResourceOwnerPasswordValidator(
            IUserRepository userRepository,
            UserManager<User> userManager)
        {
            _userRepository = userRepository;
            _userManager = userManager;
        }

        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var user = await _userRepository.GetByEmailAsync(context.UserName.ToLowerInvariant());
            if(user != null)
            {
                if(await _userManager.CheckPasswordAsync(user, context.Password))
                {
                    // TODO: proper claims and auth method
                    context.Result = new GrantValidationResult(subject: user.Id.ToString(), authenticationMethod: "Application",
                         identityProvider: "bitwarden", claims: new Claim[] { new Claim(ClaimTypes.AuthenticationMethod, "Application") });
                    return;
                }
            }

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Username or password is incorrect.");
        }
    }
}
