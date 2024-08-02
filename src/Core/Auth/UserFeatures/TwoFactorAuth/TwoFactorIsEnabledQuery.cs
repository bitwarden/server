using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public class TwoFactorIsEnabledQuery : ITwoFactorIsEnabledQuery
{
    private readonly IUserRepository _userRepository;

    public TwoFactorIsEnabledQuery(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds)
    {
        var userDetails = await _userRepository.GetManyWithCalculatedPremiumAsync(userIds.ToList());
        var result = new List<(Guid userId, bool hasTwoFactor)>();

        foreach (var userDetail in userDetails)
        {
            var providers = userDetail.GetTwoFactorProviders();
            var hasTwoFactor = false;

            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    if (provider.Value?.Enabled ?? false)
                    {
                        if (!TwoFactorProvider.RequiresPremium(provider.Key))
                        {
                            hasTwoFactor = true;
                            break;
                        }
                        if (userDetail.HasPremiumAccess)
                        {
                            hasTwoFactor = true;
                            break;
                        }
                    }
                }
            }

            result.Add((userDetail.Id, hasTwoFactor));
        }

        return result;
    }

    public async Task<IEnumerable<(T user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync<T>(IEnumerable<T> users) where T : ITwoFactorProvidersUser
    {
        var userIds = users
            .Select(u => u.GetUserId())
            .Where(u => u.HasValue)
            .Select(u => u.Value)
            .ToList();

        var twoFactorResults = await TwoFactorIsEnabledAsync(userIds);

        var result = new List<(T user, bool twoFactorIsEnabled)>();

        foreach (var user in users)
        {
            var userId = user.GetUserId();
            if (userId.HasValue)
            {
                var hasTwoFactor = twoFactorResults.FirstOrDefault(res => res.userId == userId.Value).twoFactorIsEnabled;
                result.Add((user, hasTwoFactor));
            }
            else
            {
                result.Add((user, false));
            }
        }

        return result;
    }
}
