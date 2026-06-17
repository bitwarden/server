using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class UpdatingPaymentMethodTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task BankAccount_WhenSetupIntentExists_AttachesItToTheCustomer()
    {
        const string email = "update-payment-method-bank-account@example.com";

        var (client, _, organizationId, _) = await fixture.PrepareOrganizationOwnerAsync(email);
        var bankAccountToken = await fixture.CreateConfirmedBankAccountSetupIntentAsync(email);

        // Drives UpdatePaymentMethodCommand.AddBankAccountAsync, which calls
        // ListSetupIntentsAsync with Expand=data.payment_method to find the
        // confirmed intent and then attaches it to the subscriber's customer.
        // MaskedPaymentMethod.From(SetupIntent) reads setupIntent.PaymentMethod
        // .UsBankAccount.BankName/Last4, which requires the expand.
        var response = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/payment-method",
            new
            {
                Type = "bankAccount",
                Token = bankAccountToken,
            });
        await Assert.SuccessResponseAsync(response);

        var paymentMethod = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("bankAccount", paymentMethod["type"]!.GetValue<string>());
        Assert.NotNull(paymentMethod["bankName"]);
        Assert.NotNull(paymentMethod["last4"]);
    }
}
