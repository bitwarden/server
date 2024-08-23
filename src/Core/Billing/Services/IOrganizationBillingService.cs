using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

public interface IOrganizationBillingService
{
    Task<OrganizationMetadata> GetMetadata(Guid organizationId);
}
