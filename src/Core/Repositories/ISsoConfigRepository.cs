using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface ISsoConfigRepository : IRepository<SsoConfig, long>
{
    Task<SsoConfig> GetByOrganizationIdAsync(Guid organizationId);
    Task<SsoConfig> GetByIdentifierAsync(string identifier);
    Task<ICollection<SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore);
}
