using Microsoft.AspNetCore.Identity;

namespace Bit.Admin.IdentityServer;

public abstract class ReadOnlyIdentityUserStore :
    IUserEmailStore<IdentityUser>,
    IUserSecurityStampStore<IdentityUser>
{
    public void Dispose() { }

    public Task<IdentityResult> CreateAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> DeleteAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public abstract Task<IdentityUser> FindByEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken = default);

    public abstract Task<IdentityUser> FindByIdAsync(string userId,
        CancellationToken cancellationToken = default);

    public async Task<IdentityUser> FindByNameAsync(string normalizedUserName,
        CancellationToken cancellationToken = default)
    {
        return await FindByEmailAsync(normalizedUserName, cancellationToken);
    }

    public Task<string> GetEmailAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task<string> GetNormalizedEmailAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user.Email);
    }

    public Task<string> GetNormalizedUserNameAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user.Email);
    }

    public Task<string> GetUserIdAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user.Id);
    }

    public Task<string> GetUserNameAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user.Email);
    }

    public Task SetEmailAsync(IdentityUser user, string email,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetNormalizedEmailAsync(IdentityUser user, string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.FromResult(0);
    }

    public Task SetNormalizedUserNameAsync(IdentityUser user, string normalizedName,
        CancellationToken cancellationToken = default)
    {
        user.NormalizedUserName = normalizedName;
        return Task.FromResult(0);
    }

    public Task SetUserNameAsync(IdentityUser user, string userName,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> UpdateAsync(IdentityUser user,
        CancellationToken cancellationToken = default)
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
