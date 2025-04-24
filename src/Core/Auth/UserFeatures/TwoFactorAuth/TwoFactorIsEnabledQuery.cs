using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public class TwoFactorIsEnabledQuery(IUserRepository userRepository) : ITwoFactorIsEnabledQuery
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds)
    {
        var result = new List<(Guid userId, bool hasTwoFactor)>();
        if (userIds == null || !userIds.Any())
        {
            return result;
        }

        var userDetails = await _userRepository.GetManyWithCalculatedPremiumAsync(userIds.ToList());

        foreach (var userDetail in userDetails)
        {
            var providers = userDetail.GetTwoFactorProviders();
            // if providers are null then two factor is not enabled
            if (providers == null)
            {
                result.Add((userDetail.Id, false));
                continue;
            }

            // Get all enabled providers
            var enabledProviderKeys = from provider in providers
                                        where provider.Value?.Enabled ?? false
                                        select provider.Key;

            // If no providers are enabled then two factor is not enabled
            if (!enabledProviderKeys.Any())
            {
                result.Add((userDetail.Id, false));
                continue;
            }

            // check if user only has premium two factor options
            var onlyHasPremiumTwoFactor = enabledProviderKeys.All(TwoFactorProvider.RequiresPremium);
            // If the user only has premium two factor options then their two factor is dictated by their access to premium
            if (onlyHasPremiumTwoFactor){
                result.Add((userDetail.Id, userDetail.HasPremiumAccess));
                continue;
            }

            // if providers are not null, all providers are enabled, and user has non-premium two factor options then two factor is enabled
            result.Add((userDetail.Id, true));
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
        return (await TwoFactorIsEnabledAsync([user])).First().twoFactorIsEnabled;
    }
}
