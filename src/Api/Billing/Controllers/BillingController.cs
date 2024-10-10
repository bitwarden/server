using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("billing")]
[Authorize("Application")]
public class BillingController(
    IStripeAdapter stripeAdapter) : Controller
{
    [HttpPost]
    [Route("calculate-tax")]
    [AllowAnonymous]
    public async Task<IResult> CalculateTaxAsync([FromBody] CalculateTaxRequestModel requestBody)
    {
        var options = new Stripe.Tax.CalculationCreateOptions
        {
            Currency = "usd",
            CustomerDetails = new()
            {
                Address = new()
                {
                    PostalCode = requestBody.BillingAddressPostalCode,
                    Country = requestBody.BillingAddressCountry
                },
                AddressSource = "billing"
            },
            LineItems = new()
            {
                new()
                {
                    Amount = Convert.ToInt64(requestBody.Amount * 100),
                    Reference = "Subscription",
                },
            }
        };
        var taxCalculation = await stripeAdapter.CalculateTaxAsync(options);
        var response = new CalculateTaxResponseModel
        {
            SalesTaxRate = taxCalculation.TaxBreakdown.Any()
                ? decimal.Parse(taxCalculation.TaxBreakdown.Single().TaxRateDetails.PercentageDecimal) / 100
                : 0,
            SalesTaxAmount = Convert.ToDecimal(taxCalculation.TaxAmountExclusive) / 100,
            TaxableAmount = Convert.ToDecimal(requestBody.Amount),
            TotalAmount = Convert.ToDecimal(taxCalculation.AmountTotal) / 100,
        };
        return TypedResults.Ok(response);
    }
}
