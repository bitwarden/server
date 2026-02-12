// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public class TwoFactorIsEnabledQuery : ITwoFactorIsEnabledQuery
{
    private readonly IUserRepository _userRepository;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;

    public TwoFactorIsEnabledQuery(
        IUserRepository userRepository,
        IHasPremiumAccessQuery hasPremiumAccessQuery)
    {
        _userRepository = userRepository;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
    }

    public async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds)
    {
        var result = new List<(Guid userId, bool hasTwoFactor)>();
        if (userIds == null || !userIds.Any())
        {
            return result;
        }

        var users = await _userRepository.GetManyAsync([.. userIds]);

        // Get enabled providers for each user
        var usersTwoFactorProvidersMap = users.ToDictionary(u => u.Id, GetEnabledTwoFactorProviders);

        // Bulk fetch premium status only for users who need it (those with only premium providers)
        var userIdsNeedingPremium = usersTwoFactorProvidersMap
            .Where(kvp => kvp.Value.Any() && kvp.Value.All(TwoFactorProvider.RequiresPremium))
            .Select(kvp => kvp.Key)
            .ToList();

        var premiumStatusMap = userIdsNeedingPremium.Count > 0
            ? await _hasPremiumAccessQuery.HasPremiumAccessAsync(userIdsNeedingPremium)
            : new Dictionary<Guid, bool>();

        foreach (var user in users)
        {
            var userTwoFactorProviders = usersTwoFactorProvidersMap[user.Id];

            if (!userTwoFactorProviders.Any())
            {
                result.Add((user.Id, false));
                continue;
            }

            // User has providers. If they're in the premium check map, verify premium status
            var twoFactorIsEnabled = !premiumStatusMap.TryGetValue(user.Id, out var hasPremium) || hasPremium;
            result.Add((user.Id, twoFactorIsEnabled));
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

    public async Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        var userEntity = user as User ?? await _userRepository.GetByIdAsync(userId.Value);
        if (userEntity == null)
        {
            throw new NotFoundException();
        }

        var enabledProviders = GetEnabledTwoFactorProviders(userEntity);
        if (!enabledProviders.Any())
        {
            return false;
        }

        // If all providers require premium, check if user has premium access
        if (enabledProviders.All(TwoFactorProvider.RequiresPremium))
        {
            return await _hasPremiumAccessQuery.HasPremiumAccessAsync(userId.Value);
        }

        // User has at least one non-premium provider
        return true;
    }

    /// <summary>
    /// Gets all enabled two-factor provider types for a user.
    /// </summary>
    /// <param name="user">user with two factor providers</param>
    /// <returns>list of enabled provider types</returns>
    private static IList<TwoFactorProviderType> GetEnabledTwoFactorProviders(User user)
    {
        var providers = user.GetTwoFactorProviders();

        if (providers == null || providers.Count == 0)
        {
            return Array.Empty<TwoFactorProviderType>();
        }

        // TODO: PM-21210: In practice we don't save disabled providers to the database, worth looking into.
        return (from provider in providers
                where provider.Value?.Enabled ?? false
                select provider.Key).ToList();
    }
}
