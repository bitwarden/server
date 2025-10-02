using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Clients;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Invoice = BitPayLight.Models.Invoice.Invoice;

namespace Bit.Core.Test.Billing.Payment.Commands;

using static BitPayConstants;

public class CreateBitPayInvoiceForCreditCommandTests
{
    private readonly IBitPayClient _bitPayClient = Substitute.For<IBitPayClient>();
    private readonly GlobalSettings _globalSettings = new()
    {
        BitPay = new GlobalSettings.BitPaySettings
        {
            NotificationUrl = "https://example.com/bitpay/notification",
            WebhookKey = "test-webhook-key"
        }
    };
    private const string _redirectUrl = "https://bitwarden.com/redirect";
    private readonly CreateBitPayInvoiceForCreditCommand _command;

    public CreateBitPayInvoiceForCreditCommandTests()
    {
        _command = new CreateBitPayInvoiceForCreditCommand(
            _bitPayClient,
            _globalSettings,
            Substitute.For<ILogger<CreateBitPayInvoiceForCreditCommand>>());
    }

    [Fact]
    public async Task Run_User_CreatesInvoice_ReturnsInvoiceUrl()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@gmail.com" };

        _bitPayClient.CreateInvoice(Arg.Is<Invoice>(options =>
            options.Buyer.Email == user.Email &&
            options.Buyer.Name == user.Email &&
            options.NotificationUrl == $"{_globalSettings.BitPay.NotificationUrl}?key={_globalSettings.BitPay.WebhookKey}" &&
            options.PosData == $"userId:{user.Id},{PosDataKeys.AccountCredit}" &&
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            options.Price == Convert.ToDouble(10M) &&
            options.RedirectUrl == _redirectUrl)).Returns(new Invoice { Url = "https://bitpay.com/invoice/123" });

        var result = await _command.Run(user, 10M, _redirectUrl);

        Assert.True(result.IsT0);
        var invoiceUrl = result.AsT0;
        Assert.Equal("https://bitpay.com/invoice/123", invoiceUrl);
    }

    [Fact]
    public async Task Run_Organization_CreatesInvoice_ReturnsInvoiceUrl()
    {
        var organization = new Organization { Id = Guid.NewGuid(), BillingEmail = "organization@example.com", Name = "Organization" };

        _bitPayClient.CreateInvoice(Arg.Is<Invoice>(options =>
            options.Buyer.Email == organization.BillingEmail &&
            options.Buyer.Name == organization.Name &&
            options.NotificationUrl == $"{_globalSettings.BitPay.NotificationUrl}?key={_globalSettings.BitPay.WebhookKey}" &&
            options.PosData == $"organizationId:{organization.Id},{PosDataKeys.AccountCredit}" &&
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            options.Price == Convert.ToDouble(10M) &&
            options.RedirectUrl == _redirectUrl)).Returns(new Invoice { Url = "https://bitpay.com/invoice/123" });

        var result = await _command.Run(organization, 10M, _redirectUrl);

        Assert.True(result.IsT0);
        var invoiceUrl = result.AsT0;
        Assert.Equal("https://bitpay.com/invoice/123", invoiceUrl);
    }

    [Fact]
    public async Task Run_Provider_CreatesInvoice_ReturnsInvoiceUrl()
    {
        var provider = new Provider { Id = Guid.NewGuid(), BillingEmail = "organization@example.com", Name = "Provider" };

        _bitPayClient.CreateInvoice(Arg.Is<Invoice>(options =>
            options.Buyer.Email == provider.BillingEmail &&
            options.Buyer.Name == provider.Name &&
            options.NotificationUrl == $"{_globalSettings.BitPay.NotificationUrl}?key={_globalSettings.BitPay.WebhookKey}" &&
            options.PosData == $"providerId:{provider.Id},{PosDataKeys.AccountCredit}" &&
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            options.Price == Convert.ToDouble(10M) &&
            options.RedirectUrl == _redirectUrl)).Returns(new Invoice { Url = "https://bitpay.com/invoice/123" });

        var result = await _command.Run(provider, 10M, _redirectUrl);

        Assert.True(result.IsT0);
        var invoiceUrl = result.AsT0;
        Assert.Equal("https://bitpay.com/invoice/123", invoiceUrl);
    }
}
