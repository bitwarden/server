using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Premium.Queries;

/// <summary>
/// Query for checking premium access status for users using the existing stored procedure
/// that calculates premium access from personal subscriptions and organization memberships.
/// </summary>
public class HasPremiumAccessQuery : IHasPremiumAccessQuery
{
    private readonly IUserRepository _userRepository;

    public HasPremiumAccessQuery(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> HasPremiumAccessAsync(Guid userId)
    {
        var user = await _userRepository.GetCalculatedPremiumAsync(userId);
        if (user == null)
        {
            throw new NotFoundException();
        }
        return user.HasPremiumAccess;
    }

    public async Task<Dictionary<Guid, bool>> HasPremiumAccessAsync(IEnumerable<Guid> userIds)
    {
        var usersWithPremium = await _userRepository.GetManyWithCalculatedPremiumAsync(userIds);

        if (usersWithPremium.Count() != userIds.Count())
        {
            throw new NotFoundException();
        }

        return usersWithPremium.ToDictionary(u => u.Id, u => u.HasPremiumAccess);
    }

    public async Task<bool> HasPremiumFromOrganizationAsync(Guid userId)
    {
        var user = await _userRepository.GetCalculatedPremiumAsync(userId);
        if (user == null)
        {
            throw new NotFoundException();
        }

        // Has org premium if has premium access but not personal premium
        return user.HasPremiumAccess && !user.Premium;
    }
}



