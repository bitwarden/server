﻿using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IProviderOrganizationRepository : IRepository<ProviderOrganization, Guid>
{
    Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId);
    Task<ProviderOrganizationProviderDetails> GetProviderDetailsByOrganizationAsync(Guid organizationId);
    Task<ProviderOrganization> GetByOrganizationId(Guid organizationId);
}
