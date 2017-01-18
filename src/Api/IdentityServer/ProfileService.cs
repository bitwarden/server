using IdentityServer4.Services;
using System.Threading.Tasks;
using IdentityServer4.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Api.IdentityServer
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
            context.AddFilteredClaims(context.Subject.Claims);
            return Task.FromResult(0);
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            context.IsActive = true;
            return Task.FromResult(0);
        }
    }
}
