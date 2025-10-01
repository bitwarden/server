using Bit.Core.Billing.Organizations.Models;

namespace Bit.Core.Billing.Caches;

public interface IOrganizationMetadataCache
{
    Task<OrganizationMetadata?> Get(Guid organizationId);
    Task Set(Guid organizationId, OrganizationMetadata metadata);
    Task Remove(Guid organizationId);
}
