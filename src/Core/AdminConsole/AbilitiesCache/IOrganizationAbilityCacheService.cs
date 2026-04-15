using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public interface IOrganizationAbilityCacheService
{
    Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId);
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
}
