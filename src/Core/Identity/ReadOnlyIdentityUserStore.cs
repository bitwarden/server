using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Identity
{
    public abstract class ReadOnlyIdentityUserStore :
        IUserStore<IdentityUser>,
        IUserEmailStore<IdentityUser>,
        IUserSecurityStampStore<IdentityUser>
    {
        public void Dispose() { }

        public Task<IdentityResult> CreateAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> DeleteAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public abstract Task<IdentityUser> FindByEmailAsync(string normalizedEmail,
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<IdentityUser> FindByIdAsync(string userId,
            CancellationToken cancellationToken = default(CancellationToken));

        public async Task<IdentityUser> FindByNameAsync(string normalizedUserName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await FindByEmailAsync(normalizedUserName, cancellationToken);
        }

        public Task<string> GetEmailAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.EmailConfirmed);
        }

        public Task<string> GetNormalizedEmailAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<string> GetNormalizedUserNameAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<string> GetUserIdAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Id);
        }

        public Task<string> GetUserNameAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task SetEmailAsync(IdentityUser user, string email,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SetNormalizedEmailAsync(IdentityUser user, string normalizedEmail,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            user.NormalizedEmail = normalizedEmail;
            return Task.FromResult(0);
        }

        public Task SetNormalizedUserNameAsync(IdentityUser user, string normalizedName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            user.NormalizedUserName = normalizedName;
            return Task.FromResult(0);
        }

        public Task SetUserNameAsync(IdentityUser user, string userName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> UpdateAsync(IdentityUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetSecurityStampAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.SecurityStamp);
        }
    }
}
