// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public class TwoFactorIsEnabledQuery : ITwoFactorIsEnabledQuery
{
    private readonly IUserRepository _userRepository;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;
    private readonly IFeatureService _featureService;

    public TwoFactorIsEnabledQuery(
        IUserRepository userRepository,
        IHasPremiumAccessQuery hasPremiumAccessQuery,
        IFeatureService featureService)
    {
        _userRepository = userRepository;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
        _featureService = featureService;
    }

    public async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.PremiumAccessQuery))
        {
            return await TwoFactorIsEnabledVNextAsync(userIds);
        }

        var result = new List<(Guid userId, bool hasTwoFactor)>();
        if (userIds == null || !userIds.Any())
        {
            return result;
        }

        var userDetails = await _userRepository.GetManyWithCalculatedPremiumAsync([.. userIds]);
        foreach (var userDetail in userDetails)
        {
            result.Add(
                (userDetail.Id,
                 await TwoFactorEnabledAsync(userDetail.GetTwoFactorProviders(),
                                () => Task.FromResult(userDetail.HasPremiumAccess))
                )
            );
        }

        return result;
    }

    public async Task<IEnumerable<(T user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync<T>(IEnumerable<T> users) where T : ITwoFactorProvidersUser
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.PremiumAccessQuery))
        {
            return await TwoFactorIsEnabledVNextAsync(users);
        }

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
        if (_featureService.IsEnabled(FeatureFlagKeys.PremiumAccessQuery))
        {
            var userId = user.GetUserId();
            if (!userId.HasValue)
            {
                return false;
            }

            var userEntity = user as User ?? await _userRepository.GetByIdAsync(userId.Value);
            if (userEntity == null)
            {
                return false;
            }

            return await TwoFactorIsEnabledVNextAsync(userEntity);
        }

        var id = user.GetUserId();
        if (!id.HasValue)
        {
            return false;
        }

        return await TwoFactorEnabledAsync(
            user.GetTwoFactorProviders(),
            async () =>
            {
                var calcUser = await _userRepository.GetCalculatedPremiumAsync(id.Value);
                return calcUser?.HasPremiumAccess ?? false;
            });
    }

    private async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledVNextAsync(IEnumerable<Guid> userIds)
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

    private async Task<IEnumerable<(T user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledVNextAsync<T>(IEnumerable<T> users)
        where T : ITwoFactorProvidersUser
    {
        var userIds = users
            .Select(u => u.GetUserId())
            .Where(u => u.HasValue)
            .Select(u => u.Value)
            .ToList();

        var twoFactorResults = await TwoFactorIsEnabledVNextAsync(userIds);

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

    private async Task<bool> TwoFactorIsEnabledVNextAsync(User user)
    {
        var enabledProviders = GetEnabledTwoFactorProviders(user);

        if (!enabledProviders.Any())
        {
            return false;
        }

        // If all providers require premium, check if user has premium access
        if (enabledProviders.All(TwoFactorProvider.RequiresPremium))
        {
            return await _hasPremiumAccessQuery.HasPremiumAccessAsync(user.Id);
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

    /// <summary>
    /// Checks to see what kind of two-factor is enabled.
    /// We use a delegate to check if the user has premium access, since there are multiple ways to
    /// determine if a user has premium access.
    /// </summary>
    /// <param name="providers">dictionary of two factor providers</param>
    /// <param name="hasPremiumAccessDelegate">function to check if the user has premium access</param>
    /// <returns> true if the user has two factor enabled; false otherwise;</returns>
    private async static Task<bool> TwoFactorEnabledAsync(
        Dictionary<TwoFactorProviderType, TwoFactorProvider> providers,
        Func<Task<bool>> hasPremiumAccessDelegate)
    {
        // If there are no providers, then two factor is not enabled
        if (providers == null || providers.Count == 0)
        {
            return false;
        }

        // Get all enabled providers
        // TODO: PM-21210: In practice we don't save disabled providers to the database, worth looking into.
        var enabledProviderKeys = from provider in providers
                                  where provider.Value?.Enabled ?? false
                                  select provider.Key;

        // If no providers are enabled then two factor is not enabled
        if (!enabledProviderKeys.Any())
        {
            return false;
        }

        // If there are only premium two factor options then standard two factor is not enabled
        var onlyHasPremiumTwoFactor = enabledProviderKeys.All(TwoFactorProvider.RequiresPremium);
        if (onlyHasPremiumTwoFactor)
        {
            // There are no Standard two factor options, check if the user has premium access
            // If the user has premium access, then two factor is enabled
            var premiumAccess = await hasPremiumAccessDelegate();
            return premiumAccess;
        }

        // The user has at least one non-premium two factor option
        return true;
    }
}
