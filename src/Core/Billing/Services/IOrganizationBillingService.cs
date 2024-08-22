using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

#nullable enable

public interface IOrganizationBillingService
{
    Task<OrganizationMetadata?> GetMetadata(Guid organizationId);
}
