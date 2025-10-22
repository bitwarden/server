using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class PreviewPremiumTaxCommandTests
{
    private readonly ILogger<PreviewPremiumTaxCommand> _logger = Substitute.For<ILogger<PreviewPremiumTaxCommand>>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly PreviewPremiumTaxCommand _command;

    public PreviewPremiumTaxCommandTests()
    {
        _command = new PreviewPremiumTaxCommand(_logger, _stripeAdapter);
    }

    [Fact]
    public async Task Run_PremiumWithoutStorage_ReturnsCorrectTaxAmounts()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(0, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_PremiumWithAdditionalStorage_ReturnsCorrectTaxAmounts()
    {
        var billingAddress = new BillingAddress
        {
            Country = "CA",
            PostalCode = "K1A 0A6"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(5, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 5)));
    }

    [Fact]
    public async Task Run_PremiumWithZeroStorage_ExcludesStorageFromItems()
    {
        var billingAddress = new BillingAddress
        {
            Country = "GB",
            PostalCode = "SW1A 1AA"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 250 }],
            Total = 2750
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(0, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(2.50m, tax);
        Assert.Equal(27.50m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_PremiumWithLargeStorage_HandlesMultipleStorageUnits()
    {
        var billingAddress = new BillingAddress
        {
            Country = "DE",
            PostalCode = "10115"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 800 }],
            Total = 8800
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(20, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(8.00m, tax);
        Assert.Equal(88.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 20)));
    }

    [Fact]
    public async Task Run_PremiumInternationalAddress_UsesCorrectAddressInfo()
    {
        var billingAddress = new BillingAddress
        {
            Country = "AU",
            PostalCode = "2000"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 450 }],
            Total = 4950
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(10, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(4.50m, tax);
        Assert.Equal(49.50m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "AU" &&
            options.CustomerDetails.Address.PostalCode == "2000" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 10)));
    }

    [Fact]
    public async Task Run_PremiumNoTax_ReturnsZeroTax()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "97330" // Example of a tax-free jurisdiction
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 3000
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(0, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(0.00m, tax);
        Assert.Equal(30.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "97330" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_NegativeStorage_TreatedAsZero()
    {
        var billingAddress = new BillingAddress
        {
            Country = "FR",
            PostalCode = "75001"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(-5, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "FR" &&
            options.CustomerDetails.Address.PostalCode == "75001" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_AmountConversion_CorrectlyConvertsStripeAmounts()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        // Stripe amounts are in cents
        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 123 }], // $1.23
            Total = 3123 // $31.23
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(0, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(1.23m, tax);
        Assert.Equal(31.23m, total);
    }
}
