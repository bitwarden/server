using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IProviderOrganizationRepository : IRepository<ProviderOrganization, Guid>
{
    Task<ICollection<ProviderOrganizationUnassignedOrganizationDetails>> SearchAsync(string name, string ownerEmail, int skip, int take);
    Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId);
    Task<ProviderOrganization> GetByOrganizationId(Guid organizationId);
}
