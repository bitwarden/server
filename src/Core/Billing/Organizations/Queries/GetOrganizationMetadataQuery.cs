using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;

namespace Bit.Core.Billing.Organizations.Queries;

public interface IGetOrganizationMetadataQuery
{
    Task<OrganizationMetadata?> Run(Organization organization);
}

public class GetOrganizationMetadataQuery(
    IOrganizationBillingService organizationBillingService) : IGetOrganizationMetadataQuery
{
    public async Task<OrganizationMetadata?> Run(Organization organization)
    {
        if (organization == null)
        {
            return null;
        }

        return await organizationBillingService.GetMetadata(organization.Id);
    }
}
