using Bit.Api.Billing.Models.Responses;
using Bit.Core.Services;
using Bit.Core.Utilities;
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

    [HttpGet]
    [AllowAnonymous]
    [Route("~/tax/id-types")]
    public IResult GetTaxIdTypes()
    {
        var response = TaxIdTypesResponse.From(StaticStore.SupportedTaxIdTypes);
        return TypedResults.Ok(response);
    }
}
