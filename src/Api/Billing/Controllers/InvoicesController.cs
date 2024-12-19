using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Api.Requests.Organizations;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("invoices")]
[Authorize("Application")]
public class InvoicesController : BaseBillingController
{
    [HttpPost("preview-organization")]
    public async Task<IResult> PreviewInvoiceAsync(
        [FromBody] PreviewOrganizationInvoiceRequestBody model,
        [FromServices] ICurrentContext currentContext,
        [FromServices] IOrganizationRepository organizationRepository,
        [FromServices] IPaymentService paymentService)
    {
        Organization organization = null;
        if (model.OrganizationId != default)
        {
            if (!await currentContext.EditPaymentMethods(model.OrganizationId))
            {
                return Error.Unauthorized();
            }

            organization = await organizationRepository.GetByIdAsync(model.OrganizationId);
            if (organization == null)
            {
                return Error.NotFound();
            }
        }

        var invoice = await paymentService.PreviewInvoiceAsync(model, organization?.GatewayCustomerId,
            organization?.GatewaySubscriptionId);

        return TypedResults.Ok(invoice);
    }
}
