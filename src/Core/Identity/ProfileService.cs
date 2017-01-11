using IdentityServer4.Services;
using System.Threading.Tasks;
using IdentityServer4.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;
using System.Security.Claims;

namespace Bit.Core.Identity
{
    public class ProfileService : IProfileService
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;

        public ProfileService(
            IUserRepository userRepository,
            IUserService userService)
        {
            _userRepository = userRepository;
            _userService = userService;
        }

        public Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            // TODO: load proper claims for user
            context.AddFilteredClaims(new Claim[] { new Claim(ClaimTypes.AuthenticationMethod, "Application") });
            return Task.FromResult(0);
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            context.IsActive = true;
            return Task.FromResult(0);
        }
    }
}
