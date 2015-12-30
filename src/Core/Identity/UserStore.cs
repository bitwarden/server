using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Bit.Core.Domains;
using Bit.Core.Repositories;

namespace Bit.Core.Identity
{
    public class UserStore :
        IUserStore<User>,
        IUserPasswordStore<User>,
        IUserEmailStore<User>,
        IUserTwoFactorStore<User>,
        IUserSecurityStampStore<User>
    {
        private readonly IUserRepository _userRepository;
        private readonly CurrentContext _currentContext;

        public UserStore(
            IUserRepository userRepository,
            CurrentContext currentContext)
        {
            _userRepository = userRepository;
            _currentContext = currentContext;
        }

        public void Dispose() { }

        public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _userRepository.CreateAsync(user);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _userRepository.DeleteAsync(user);
            return IdentityResult.Success;
        }

        public async Task<User> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default(CancellationToken))
        {
            if(_currentContext?.User != null && _currentContext.User.Email == normalizedEmail)
            {
                return _currentContext.User;
            }

            return await _userRepository.GetByEmailAsync(normalizedEmail);
        }

        public async Task<User> FindByIdAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if(_currentContext?.User != null && _currentContext.User.Id == userId)
            {
                return _currentContext.User;
            }

            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<User> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default(CancellationToken))
        {
            if(_currentContext?.User != null && _currentContext.User.Email == normalizedUserName)
            {
                return _currentContext.User;
            }

            return await _userRepository.GetByEmailAsync(normalizedUserName);
        }

        public Task<string> GetEmailAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true); // all emails are confirmed
        }

        public Task<string> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<string> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<string> GetPasswordHashAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.MasterPassword);
        }

        public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Id);
        }

        public Task<string> GetUserNameAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(user.Email);
        }

        public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(user.MasterPassword));
        }

        public Task SetEmailAsync(User user, string email, CancellationToken cancellationToken = default(CancellationToken))
        {
            user.Email = email;
            return Task.FromResult(0);
        }

        public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken = default(CancellationToken))
        {
            // do nothing
            return Task.FromResult(0);
        }

        public Task SetNormalizedEmailAsync(User user, string normalizedEmail, CancellationToken cancellationToken = default(CancellationToken))
        {
            user.Email = normalizedEmail;
            return Task.FromResult(0);
        }

        public Task SetNormalizedUserNameAsync(User user, string normalizedName, CancellationToken cancellationToken = default(CancellationToken))
        {
            user.Email = normalizedName;
            return Task.FromResult(0);
        }

        public Task SetPasswordHashAsync(User user, string passwordHash, CancellationToken cancellationToken = default(CancellationToken))
        {
            user.MasterPassword = passwordHash;
            return Task.FromResult(0);
        }

        public Task SetUserNameAsync(User user, string userName, CancellationToken cancellationToken = default(CancellationToken))
        {
            user.Email = userName;
            return Task.FromResult(0);
        }

        public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _userRepository.ReplaceAsync(user);
            return IdentityResult.Success;
        }

        public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
        {
            user.TwoFactorEnabled = enabled;
            return Task.FromResult(0);
        }

        public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.TwoFactorEnabled && user.TwoFactorProvider.HasValue);
        }

        public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
        {
            user.SecurityStamp = stamp;
            return Task.FromResult(0);
        }

        public Task<string> GetSecurityStampAsync(User user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.SecurityStamp);
        }
    }
}
