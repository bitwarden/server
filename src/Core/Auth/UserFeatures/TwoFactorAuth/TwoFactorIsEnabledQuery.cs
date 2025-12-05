// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.PremiumAccess;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public class TwoFactorIsEnabledQuery : ITwoFactorIsEnabledQuery
{
    private readonly IUserRepository _userRepository;
    private readonly IPremiumAccessQuery _premiumAccessQuery;

    public TwoFactorIsEnabledQuery(
        IUserRepository userRepository,
        IPremiumAccessQuery premiumAccessQuery)
    {
        _userRepository = userRepository;
        _premiumAccessQuery = premiumAccessQuery;
    }

    public async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds)
    {
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

        return await TwoFactorEnabledAsync(
            user.GetTwoFactorProviders(),
            async () =>
            {
                var calcUser = await _userRepository.GetCalculatedPremiumAsync(userId.Value);
                return calcUser?.HasPremiumAccess ?? false;
            });
    }

    public async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledVNextAsync(IEnumerable<Guid> userIds)
    {
        var result = new List<(Guid userId, bool hasTwoFactor)>();
        if (userIds == null || !userIds.Any())
        {
            return result;
        }

        var users = await _userRepository.GetManyAsync([.. userIds]);
        var premiumStatus = await _premiumAccessQuery.CanAccessPremiumAsync(users);

        foreach (var user in users)
        {
            result.Add(
                (user.Id,
                 await TwoFactorEnabledAsync(
                     user.GetTwoFactorProviders(),
                     () => Task.FromResult(premiumStatus.GetValueOrDefault(user.Id, false))
                 ))
            );
        }

        return result;
    }

    public async Task<IEnumerable<(T user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledVNextAsync<T>(IEnumerable<T> users)
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

    public async Task<bool> TwoFactorIsEnabledVNextAsync(ITwoFactorProvidersUser user)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        // Try to get premium status without fetching User entity if possible
        bool hasPersonalPremium;
        if (user is User userEntity)
        {
            hasPersonalPremium = userEntity.Premium;
        }
        else if (user is OrganizationUserUserDetails orgUserDetails)
        {
            hasPersonalPremium = orgUserDetails.Premium.GetValueOrDefault(false);
        }
        else
        {
            // Fallback: fetch the User entity
            var fetchedUser = await _userRepository.GetByIdAsync(userId.Value);
            if (fetchedUser == null)
            {
                return false;
            }
            hasPersonalPremium = fetchedUser.Premium;
        }

        return await TwoFactorEnabledAsync(
            user.GetTwoFactorProviders(),
            async () => await _premiumAccessQuery.CanAccessPremiumAsync(userId.Value, hasPersonalPremium));
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
