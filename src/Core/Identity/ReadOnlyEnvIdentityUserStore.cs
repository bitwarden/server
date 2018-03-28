using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Identity
{
    public class ReadOnlyEnvIdentityUserStore : ReadOnlyIdentityUserStore
    {
        private readonly IConfiguration _configuration;

        public ReadOnlyEnvIdentityUserStore(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public override Task<IdentityUser> FindByEmailAsync(string normalizedEmail,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var usersCsv = _configuration["adminSettings:admins"];
            if(!CoreHelpers.SettingHasValue(usersCsv))
            {
                return Task.FromResult<IdentityUser>(null);
            }

            var users = usersCsv.ToLowerInvariant().Split(',');
            var user = users.Where(a => a.Trim() == normalizedEmail).FirstOrDefault();
            if(user == null || !user.Contains("@"))
            {
                return Task.FromResult<IdentityUser>(null);
            }

            user = user.Trim();
            return Task.FromResult(new IdentityUser
            {
                Id = user,
                Email = user,
                NormalizedEmail = user,
                EmailConfirmed = true,
                UserName = user,
                NormalizedUserName = user,
                SecurityStamp = user
            });
        }

        public override Task<IdentityUser> FindByIdAsync(string userId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return FindByEmailAsync(userId, cancellationToken);
        }
    }
}
