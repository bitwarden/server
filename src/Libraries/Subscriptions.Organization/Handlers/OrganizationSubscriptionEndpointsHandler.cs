using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class OrganizationSubscriptionEndpointsHandler(
    IOrganizationRepository organizationRepository,
    IGetSubscriptionPreviewQuery getSubscriptionPreviewQuery)
{
    public async Task<SubscriptionPreview> GetPreviewAsync(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        return await getSubscriptionPreviewQuery.Run(organization)
            ?? throw new NotFoundException();
    }
}
