using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models.Requests;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class PreviewOrganizationRestartHandler(
    IOrganizationRepository organizationRepository,
    IPreviewOrganizationRestartCommand previewOrganizationRestartCommand)
{
    public async Task<InvoicePreview> HandleAsync(Guid organizationId, PreviewOrganizationRestartRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        return await previewOrganizationRestartCommand.Run(organization, request);
    }
}
