using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface ISsoConfigRepository : IRepository<SsoConfig, long>
{
    Task<SsoConfig?> GetByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<SsoConfig>> GetManyByOrganizationIdsAsync(IEnumerable<Guid> organizationIds);
    Task<SsoConfig?> GetByIdentifierAsync(string identifier);
    Task<ICollection<SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore);
}
