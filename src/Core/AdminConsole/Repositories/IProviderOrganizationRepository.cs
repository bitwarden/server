using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.AdminConsole.Repositories;

public interface IProviderOrganizationRepository : IRepository<ProviderOrganization, Guid>
{
    Task<ICollection<ProviderOrganization>?> CreateManyAsync(IEnumerable<ProviderOrganization> providerOrganizations);
    Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId);
    Task<ProviderOrganization?> GetByOrganizationId(Guid organizationId);
    Task<IEnumerable<ProviderOrganizationProviderDetails>> GetManyByUserAsync(Guid userId);
    Task<int> GetCountByOrganizationIdsAsync(IEnumerable<Guid> organizationIds);
}
