using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface ISsoConfigRepository : IRepository<SsoConfig, long>
{
    Task<SsoConfig?> GetByOrganizationIdAsync(Guid organizationId);
    /// <summary>
    /// Gets the SSO configurations of many organizations. Also returns disabled configurations.
    /// An organization without an SSO configuration has no entry.
    /// </summary>
    Task<ICollection<SsoConfig>> GetManyByOrganizationIdsAsync(IEnumerable<Guid> organizationIds);
    Task<SsoConfig?> GetByIdentifierAsync(string identifier);
    Task<ICollection<SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore);
}
