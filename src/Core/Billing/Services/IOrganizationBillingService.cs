using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

#nullable enable

public interface IOrganizationBillingService
{
    Task<OrganizationMetadataDTO?> GetMetadata(Guid organizationId);
}
