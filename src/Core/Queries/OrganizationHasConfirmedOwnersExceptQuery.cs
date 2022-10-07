using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Queries.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.Queries;

public class OrganizationHasConfirmedOwnersExceptQuery : IOrganizationHasConfirmedOwnersExceptQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public OrganizationHasConfirmedOwnersExceptQuery(ICurrentContext currentContext, IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<bool> HasConfirmedOwnersExceptAsync(Guid organizationId, IEnumerable<Guid> organizationUsersId, bool includeProvider = true)
    {
        var confirmedOwners = await GetConfirmedOwnersAsync(organizationId);
        var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
        bool hasOtherOwner = confirmedOwnersIds.Except(organizationUsersId).Any();
        if (!hasOtherOwner && includeProvider)
        {
            return (await _currentContext.ProviderIdForOrg(organizationId)).HasValue;
        }
        return hasOtherOwner;
    }

    private async Task<IEnumerable<OrganizationUser>> GetConfirmedOwnersAsync(Guid organizationId)
    {
        var owners = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId,
            OrganizationUserType.Owner);
        return owners.Where(o => o.Status == OrganizationUserStatusType.Confirmed);
    }
}
