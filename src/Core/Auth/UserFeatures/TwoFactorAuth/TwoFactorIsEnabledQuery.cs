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

            // Get all enabled providers
            var enabledProviderKeys = from provider in providers
                                      where provider.Value?.Enabled ?? false
                                      select provider.Key;

            // Find the first provider that is enabled and passes the premium check
            var hasTwoFactor = enabledProviderKeys
                .Select(type => userDetail.HasPremiumAccess || !TwoFactorProvider.RequiresPremium(type))
                .FirstOrDefault();

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
