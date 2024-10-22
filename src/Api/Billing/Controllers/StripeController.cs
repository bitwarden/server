using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Api.Billing.Controllers;

[Authorize("Application")]
public class StripeController(
    IStripeAdapter stripeAdapter) : Controller
{
    [HttpPost]
    [Route("~/setup-intent/bank-account")]
    public async Task<Ok<string>> CreateSetupIntentForBankAccountAsync()
    {
        var options = new SetupIntentCreateOptions
        {
            PaymentMethodOptions = new SetupIntentPaymentMethodOptionsOptions
            {
                UsBankAccount = new SetupIntentPaymentMethodOptionsUsBankAccountOptions
                {
                    VerificationMethod = "microdeposits"
                }
            },
            PaymentMethodTypes = ["us_bank_account"],
            Usage = "off_session"
        };

        var setupIntent = await stripeAdapter.SetupIntentCreate(options);

        return TypedResults.Ok(setupIntent.ClientSecret);
    }

    [HttpPost]
    [Route("~/setup-intent/card")]
    public async Task<Ok<string>> CreateSetupIntentForCardAsync()
    {
        var options = new SetupIntentCreateOptions
        {
            PaymentMethodTypes = ["card"],
            Usage = "off_session"
        };

        var setupIntent = await stripeAdapter.SetupIntentCreate(options);

        return TypedResults.Ok(setupIntent.ClientSecret);
    }

    [HttpPost]
    [Route("~/tax/calculate")]
    public async Task<IResult> CalculateAsync([FromBody] CalculateTaxRequestModel requestBody)
    {
        var options = new Stripe.Tax.CalculationCreateOptions
        {
            Currency = "usd",
            CustomerDetails = new()
            {
                Address = new()
                {
                    PostalCode = requestBody.PostalCode,
                    Country = requestBody.Country
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
        try
        {
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
        catch (StripeException e)
        {
            return TypedResults.BadRequest(e.Message);
        }
    }
}
