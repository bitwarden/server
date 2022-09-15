using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Admin.IdentityServer;

public class ReadOnlyEnvIdentityUserStore : ReadOnlyIdentityUserStore
{
    private readonly IConfiguration _configuration;

    public ReadOnlyEnvIdentityUserStore(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override Task<IdentityUser> FindByEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var usersCsv = _configuration["adminSettings:admins"];
        if (!CoreHelpers.SettingHasValue(usersCsv))
        {
            return Task.FromResult<IdentityUser>(null);
        }

        var users = usersCsv.ToLowerInvariant().Split(',');
        var usersDict = new Dictionary<string, string>();
        foreach (var u in users)
        {
            var parts = u.Split(':');
            if (parts.Length == 2)
            {
                var email = parts[0].Trim();
                var stamp = parts[1].Trim();
                usersDict.Add(email, stamp);
            }
            else
            {
                var email = parts[0].Trim();
                usersDict.Add(email, email);
            }
        }

        var userStamp = usersDict.ContainsKey(normalizedEmail) ? usersDict[normalizedEmail] : null;
        if (userStamp == null)
        {
            return Task.FromResult<IdentityUser>(null);
        }

        return Task.FromResult(new IdentityUser
        {
            Id = normalizedEmail,
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            UserName = normalizedEmail,
            NormalizedUserName = normalizedEmail,
            SecurityStamp = userStamp
        });
    }

    public override Task<IdentityUser> FindByIdAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        return FindByEmailAsync(userId, cancellationToken);
    }
}
