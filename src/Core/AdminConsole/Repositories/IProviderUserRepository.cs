﻿using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.AdminConsole.Repositories;

public interface IProviderUserRepository : IRepository<ProviderUser, Guid>
{
    Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers);
    Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids);
    Task<ICollection<ProviderUser>> GetManyByUserAsync(Guid userId);
    Task<ProviderUser?> GetByProviderUserAsync(Guid providerId, Guid userId);
    Task<ICollection<ProviderUser>> GetManyByProviderAsync(
        Guid providerId,
        ProviderUserType? type = null
    );
    Task<ICollection<ProviderUserUserDetails>> GetManyDetailsByProviderAsync(
        Guid providerId,
        ProviderUserStatusType? status = null
    );
    Task<ICollection<ProviderUserProviderDetails>> GetManyDetailsByUserAsync(
        Guid userId,
        ProviderUserStatusType? status = null
    );
    Task<IEnumerable<ProviderUserOrganizationDetails>> GetManyOrganizationDetailsByUserAsync(
        Guid userId,
        ProviderUserStatusType? status = null
    );
    Task DeleteManyAsync(IEnumerable<Guid> userIds);
    Task<IEnumerable<ProviderUserPublicKey>> GetManyPublicKeysByProviderUserAsync(
        Guid providerId,
        IEnumerable<Guid> Ids
    );
    Task<int> GetCountByOnlyOwnerAsync(Guid userId);
    Task<ICollection<ProviderUser>> GetManyByOrganizationAsync(
        Guid organizationId,
        ProviderUserStatusType? status = null
    );
}
