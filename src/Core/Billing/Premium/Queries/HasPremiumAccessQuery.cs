using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Premium.Queries;

public class HasPremiumAccessQuery : IHasPremiumAccessQuery
{
    private readonly IUserRepository _userRepository;

    public HasPremiumAccessQuery(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> HasPremiumAccessAsync(Guid userId)
    {
        var user = await _userRepository.GetPremiumAccessAsync(userId);
        if (user == null)
        {
            throw new NotFoundException();
        }

        return user.HasPremiumAccess;
    }

    public async Task<Dictionary<Guid, bool>> HasPremiumAccessAsync(IEnumerable<Guid> userIds)
    {
        var distinctUserIds = userIds.Distinct().ToList();
        var usersWithPremium = await _userRepository.GetPremiumAccessByIdsAsync(distinctUserIds);

        if (usersWithPremium.Count() != distinctUserIds.Count)
        {
            throw new NotFoundException();
        }

        return usersWithPremium.ToDictionary(u => u.Id, u => u.HasPremiumAccess);
    }

    public async Task<bool> HasPremiumFromOrganizationAsync(Guid userId)
    {
        var user = await _userRepository.GetPremiumAccessAsync(userId);
        if (user == null)
        {
            throw new NotFoundException();
        }

        return user.OrganizationPremium;
    }
}
