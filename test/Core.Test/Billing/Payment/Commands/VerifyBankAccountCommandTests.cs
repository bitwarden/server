using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Commands;

public class VerifyBankAccountCommandTests
{
    private readonly ISetupIntentCache _setupIntentCache = Substitute.For<ISetupIntentCache>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly VerifyBankAccountCommand _command;

    public VerifyBankAccountCommandTests()
    {
        _command = new VerifyBankAccountCommand(
            Substitute.For<ILogger<VerifyBankAccountCommand>>(),
            _setupIntentCache,
            _stripeAdapter);
    }

    [Fact]
    public async Task Run_MakesCorrectInvocations_ReturnsMaskedBankAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        const string setupIntentId = "seti_123";

        _setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id).Returns(setupIntentId);

        var setupIntent = new SetupIntent
        {
            Id = setupIntentId,
            PaymentMethodId = "pm_123",
            PaymentMethod =
                new PaymentMethod
                {
                    Id = "pm_123",
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccount { BankName = "Chase", Last4 = "9999" }
                },
            NextAction = new SetupIntentNextAction
            {
                VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits
                {
                    HostedVerificationUrl = "https://example.com"
                }
            },
            Status = "requires_action"
        };

        _stripeAdapter.SetupIntentGet(setupIntentId,
            Arg.Is<SetupIntentGetOptions>(options => options.HasExpansions("payment_method"))).Returns(setupIntent);

        _stripeAdapter.PaymentMethodAttachAsync(setupIntent.PaymentMethodId,
                Arg.Is<PaymentMethodAttachOptions>(options => options.Customer == organization.GatewayCustomerId))
            .Returns(setupIntent.PaymentMethod);

        var result = await _command.Run(organization, "DESCRIPTOR_CODE");

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.Equal("https://example.com", maskedBankAccount.HostedVerificationUrl);

        await _stripeAdapter.Received(1).SetupIntentVerifyMicroDeposit(setupIntent.Id,
            Arg.Is<SetupIntentVerifyMicrodepositsOptions>(options => options.DescriptorCode == "DESCRIPTOR_CODE"));

        await _stripeAdapter.Received(1).CustomerUpdateAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
            options => options.InvoiceSettings.DefaultPaymentMethod == setupIntent.PaymentMethodId));
    }
}
