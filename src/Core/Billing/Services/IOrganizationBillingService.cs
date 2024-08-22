using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

#nullable enable

public interface IOrganizationBillingService
{
    Task<OrganizationMetadata?> GetMetadata(Organization organization);
}
