using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.PremiumAccess;

/// <summary>
/// Query for checking premium access status for users using cached organization abilities.
/// </summary>
public class PremiumAccessQuery : IPremiumAccessQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;

    public PremiumAccessQuery(
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService)
    {
        _organizationUserRepository = organizationUserRepository;
        _applicationCacheService = applicationCacheService;
    }

    public async Task<bool> CanAccessPremiumAsync(User user)
    {
        return await CanAccessPremiumAsync(user.Id, user.Premium);
    }

    public async Task<bool> CanAccessPremiumAsync(Guid userId, bool hasPersonalPremium)
    {
        if (hasPersonalPremium)
        {
            return true;
        }

        return await HasPremiumFromOrganizationAsync(userId);
    }

    public async Task<bool> HasPremiumFromOrganizationAsync(Guid userId)
    {
        // Note: GetManyByUserAsync only returns Accepted and Confirmed status org users
        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(userId);
        if (!orgUsers.Any())
        {
            return false;
        }

        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        return orgUsers.Any(ou =>
            orgAbilities.TryGetValue(ou.OrganizationId, out var orgAbility) &&
            orgAbility.UsersGetPremium &&
            orgAbility.Enabled);
    }

    public async Task<Dictionary<Guid, bool>> CanAccessPremiumAsync(IEnumerable<User> users)
    {
        var result = new Dictionary<Guid, bool>();
        var usersList = users.ToList();

        if (!usersList.Any())
        {
            return result;
        }

        var userIds = usersList.Select(u => u.Id).ToList();

        // Get all org memberships for these users in one query
        // Note: GetManyByManyUsersAsync only returns Accepted and Confirmed status org users
        var allOrgUsers = await _organizationUserRepository.GetManyByManyUsersAsync(userIds);
        var orgUsersGrouped = allOrgUsers
            .Where(ou => ou.UserId.HasValue)
            .GroupBy(ou => ou.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();

        foreach (var user in usersList)
        {
            var hasPersonalPremium = user.Premium;
            if (hasPersonalPremium)
            {
                result[user.Id] = true;
                continue;
            }

            var hasPremiumFromOrg = orgUsersGrouped.TryGetValue(user.Id, out var userOrgs) &&
                userOrgs.Any(ou =>
                    orgAbilities.TryGetValue(ou.OrganizationId, out var orgAbility) &&
                    orgAbility.UsersGetPremium &&
                    orgAbility.Enabled);

            result[user.Id] = hasPremiumFromOrg;
        }

        return result;
    }
}

