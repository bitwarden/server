using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class HasConfirmedOwnersExceptQuery : IHasConfirmedOwnersExceptQuery
{
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public HasConfirmedOwnersExceptQuery(
        IProviderUserRepository providerUserRepository,
        IOrganizationUserRepository organizationUserRepository
    )
    {
        _providerUserRepository = providerUserRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<bool> HasConfirmedOwnersExceptAsync(
        Guid organizationId,
        IEnumerable<Guid> organizationUsersId,
        bool includeProvider = true
    )
    {
        var confirmedOwners = await GetConfirmedOwnersAsync(organizationId);
        var confirmedOwnersIds = confirmedOwners.Select(u => u.Id);
        bool hasOtherOwner = confirmedOwnersIds.Except(organizationUsersId).Any();
        if (!hasOtherOwner && includeProvider)
        {
            return (
                await _providerUserRepository.GetManyByOrganizationAsync(
                    organizationId,
                    ProviderUserStatusType.Confirmed
                )
            ).Any();
        }
        return hasOtherOwner;
    }

    private async Task<IEnumerable<OrganizationUser>> GetConfirmedOwnersAsync(Guid organizationId)
    {
        var owners = await _organizationUserRepository.GetManyByOrganizationAsync(
            organizationId,
            OrganizationUserType.Owner
        );
        return owners.Where(o => o.Status == OrganizationUserStatusType.Confirmed);
    }
}
