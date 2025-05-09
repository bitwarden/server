using Bit.Api.Billing.Models.Requests;
using Bit.Core.Billing.Tax.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Authorize("Application")]
[Route("tax")]
public class TaxController(
    IPreviewTaxAmountCommand previewTaxAmountCommand) : BaseBillingController
{
    [HttpPost("preview-amount/organization-trial")]
    public async Task<IResult> PreviewTaxAmountForOrganizationTrialAsync(
        [FromBody] PreviewTaxAmountForOrganizationTrialRequestBody requestBody)
    {
        var parameters = new OrganizationTrialParameters
        {
            PlanType = requestBody.PlanType,
            ProductType = requestBody.ProductType,
            TaxInformation = new OrganizationTrialParameters.TaxInformationDTO
            {
                Country = requestBody.TaxInformation.Country,
                PostalCode = requestBody.TaxInformation.PostalCode,
                TaxId = requestBody.TaxInformation.TaxId
            }
        };

        var result = await previewTaxAmountCommand.Run(parameters);

        return result.Match<IResult>(
            taxAmount => TypedResults.Ok(new { TaxAmount = taxAmount }),
            badRequest => Error.BadRequest(badRequest.TranslationKey),
            unhandled => Error.ServerError(unhandled.TranslationKey));
    }
}
