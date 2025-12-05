using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.PremiumAccess;

/// <summary>
/// Query for checking premium access status for users using the existing stored procedure
/// that calculates premium access from personal subscriptions and organization memberships.
/// </summary>
public class PremiumAccessQuery : IPremiumAccessQuery
{
    private readonly IUserRepository _userRepository;

    public PremiumAccessQuery(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> CanAccessPremiumAsync(Guid userId)
    {
        var user = await _userRepository.GetCalculatedPremiumAsync(userId);
        return user?.HasPremiumAccess ?? false;
    }

    public async Task<Dictionary<Guid, bool>> CanAccessPremiumAsync(IEnumerable<Guid> userIds)
    {
        var usersWithPremium = await _userRepository.GetManyWithCalculatedPremiumAsync(userIds);
        return usersWithPremium.ToDictionary(u => u.Id, u => u.HasPremiumAccess);
    }

    public async Task<bool> HasPremiumFromOrganizationAsync(Guid userId)
    {
        var user = await _userRepository.GetCalculatedPremiumAsync(userId);
        if (user == null)
        {
            return false;
        }

        // Has org premium if has premium access but not personal premium
        return user.HasPremiumAccess && !user.Premium;
    }
}

