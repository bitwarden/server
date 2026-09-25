using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.Organization.Models.Requests;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class GetOrganizationPlanChangePreviewHandler(
    IOrganizationRepository organizationRepository,
    IGetOrganizationPlanChangePreviewQuery getOrganizationPlanChangePreviewQuery)
{
    public async Task<InvoicePreview> HandleAsync(Guid organizationId, GetOrganizationPlanChangePreviewRequest previewRequest)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        return await getOrganizationPlanChangePreviewQuery.Run(organization, previewRequest.ToDomain());
    }
}
