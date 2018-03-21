using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Repositories;

namespace Bit.Core.Identity
{
    public class ReadOnlyDatabaseIdentityUserStore : ReadOnlyIdentityUserStore
    {
        private readonly IUserRepository _userRepository;

        public ReadOnlyDatabaseIdentityUserStore(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public override async Task<IdentityUser> FindByEmailAsync(string normalizedEmail,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var user = await _userRepository.GetByEmailAsync(normalizedEmail);
            return user?.ToIdentityUser();
        }

        public override async Task<IdentityUser> FindByIdAsync(string userId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if(!Guid.TryParse(userId, out var userIdGuid))
            {
                return null;
            }

            var user = await _userRepository.GetByIdAsync(userIdGuid);
            return user?.ToIdentityUser();
        }
    }
}
