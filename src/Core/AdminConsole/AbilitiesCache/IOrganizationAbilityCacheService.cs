using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public interface IOrganizationAbilityCacheService
{
    Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds, CancellationToken cancellationToken = default);
    Task UpsertOrganizationAbilityAsync(Organization organization, CancellationToken cancellationToken = default);
    Task DeleteOrganizationAbilityAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
