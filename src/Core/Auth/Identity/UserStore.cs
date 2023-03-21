using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Identity;

public class UserStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserTwoFactorStore<User>,
    IUserSecurityStampStore<User>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;

    public UserStore(
        IServiceProvider serviceProvider,
        IUserRepository userRepository,
        ICurrentContext currentContext)
    {
        _serviceProvider = serviceProvider;
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
        if (_currentContext?.User != null && _currentContext.User.Email == normalizedEmail)
        {
            return _currentContext.User;
        }

        _currentContext.User = await _userRepository.GetByEmailAsync(normalizedEmail);
        return _currentContext.User;
    }

    public async Task<User> FindByIdAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (_currentContext?.User != null &&
            string.Equals(_currentContext.User.Id.ToString(), userId, StringComparison.InvariantCultureIgnoreCase))
        {
            return _currentContext.User;
        }

        Guid userIdGuid;
        if (!Guid.TryParse(userId, out userIdGuid))
        {
            return null;
        }

        _currentContext.User = await _userRepository.GetByIdAsync(userIdGuid);
        return _currentContext.User;
    }

    public async Task<User> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await FindByEmailAsync(normalizedUserName, cancellationToken);
    }

    public Task<string> GetEmailAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.FromResult(user.EmailVerified);
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
        return Task.FromResult(user.Id.ToString());
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
        user.EmailVerified = confirmed;
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
        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        await _userRepository.ReplaceAsync(user);
        return IdentityResult.Success;
    }

    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        // Do nothing...
        return Task.FromResult(0);
    }

    public async Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken)
    {
        return await _serviceProvider.GetRequiredService<IUserService>().TwoFactorIsEnabledAsync(user);
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
