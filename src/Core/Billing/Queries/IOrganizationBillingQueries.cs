using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Queries;

public interface IOrganizationBillingQueries
{
    Task<OrganizationMetadataDTO> GetMetadata(Guid organizationId);
}
