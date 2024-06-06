using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

public interface IOrganizationBillingService
{
    Task<OrganizationMetadataDTO> GetMetadata(Guid organizationId);
}
