using Bit.Billing.Controllers;
using Bit.Billing.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Payment.Clients;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using BitPayLight.Models.Invoice;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Transaction = Bit.Core.Entities.Transaction;

namespace Bit.Billing.Test.Controllers;

public class BitPayControllerTests
{
    private readonly GlobalSettings _globalSettings = new();
    private readonly IBitPayClient _bitPayClient = Substitute.For<IBitPayClient>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IMailService _mailService = Substitute.For<IMailService>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();

    private readonly IPremiumUserBillingService _premiumUserBillingService =
        Substitute.For<IPremiumUserBillingService>();

    private const string _validWebhookKey = "valid-webhook-key";
    private const string _invalidWebhookKey = "invalid-webhook-key";

    public BitPayControllerTests()
    {
        var bitPaySettings = new GlobalSettings.BitPaySettings { WebhookKey = _validWebhookKey };
        _globalSettings.BitPay = bitPaySettings;
    }

    private BitPayController CreateController() => new(
        _globalSettings,
        _bitPayClient,
        _transactionRepository,
        _organizationRepository,
        _userRepository,
        _providerRepository,
        _mailService,
        _paymentService,
        Substitute.For<ILogger<BitPayController>>(),
        _premiumUserBillingService);

    [Fact]
    public async Task PostIpn_InvalidKey_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();

        var result = await controller.PostIpn(eventModel, _invalidWebhookKey);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid key", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_NullKey_ThrowsException()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();

        await Assert.ThrowsAsync<ArgumentNullException>(() => controller.PostIpn(eventModel, null!));
    }

    [Fact]
    public async Task PostIpn_EmptyKey_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();

        var result = await controller.PostIpn(eventModel, string.Empty);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid key", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_NonUsdCurrency_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice(currency: "EUR");

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cannot process non-USD payments", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_NullPosData_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice(posData: null!);

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid POS data", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_EmptyPosData_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice(posData: "");

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid POS data", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_PosDataWithoutAccountCredit_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice(posData: "organizationId:550e8400-e29b-41d4-a716-446655440000");

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid POS data", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_PosDataWithoutValidId_BadRequest()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice(posData: "accountCredit:1");

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid POS data", badRequestResult.Value);
    }

    [Fact]
    public async Task PostIpn_IncompleteInvoice_Ok()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice(status: "paid");

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Waiting for invoice to be completed", okResult.Value);
    }

    [Fact]
    public async Task PostIpn_ExistingTransaction_Ok()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var invoice = CreateValidInvoice();
        var existingTransaction = new Transaction { GatewayId = invoice.Id };

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);
        _transactionRepository.GetByGatewayIdAsync(GatewayType.BitPay, invoice.Id).Returns(existingTransaction);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Invoice already processed", okResult.Value);
    }

    [Fact]
    public async Task PostIpn_ValidOrganizationTransaction_Success()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var organizationId = Guid.NewGuid();
        var invoice = CreateValidInvoice(posData: $"organizationId:{organizationId},accountCredit:1");
        var organization = new Organization { Id = organizationId, BillingEmail = "billing@example.com" };

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);
        _transactionRepository.GetByGatewayIdAsync(GatewayType.BitPay, invoice.Id).Returns((Transaction)null);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _paymentService.CreditAccountAsync(organization, Arg.Any<decimal>()).Returns(true);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        Assert.IsType<OkResult>(result);
        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == organizationId &&
            t.Type == TransactionType.Credit &&
            t.Gateway == GatewayType.BitPay &&
            t.PaymentMethodType == PaymentMethodType.BitPay));
        await _organizationRepository.Received(1).ReplaceAsync(organization);
        await _mailService.Received(1).SendAddedCreditAsync("billing@example.com", 100.00m);
    }

    [Fact]
    public async Task PostIpn_ValidUserTransaction_Success()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var userId = Guid.NewGuid();
        var invoice = CreateValidInvoice(posData: $"userId:{userId},accountCredit:1");
        var user = new User { Id = userId, Email = "user@example.com" };

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);
        _transactionRepository.GetByGatewayIdAsync(GatewayType.BitPay, invoice.Id).Returns((Transaction)null);
        _userRepository.GetByIdAsync(userId).Returns(user);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        Assert.IsType<OkResult>(result);
        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.UserId == userId &&
            t.Type == TransactionType.Credit &&
            t.Gateway == GatewayType.BitPay &&
            t.PaymentMethodType == PaymentMethodType.BitPay));
        await _premiumUserBillingService.Received(1).Credit(user, 100.00m);
        await _mailService.Received(1).SendAddedCreditAsync("user@example.com", 100.00m);
    }

    [Fact]
    public async Task PostIpn_ValidProviderTransaction_Success()
    {
        var controller = CreateController();
        var eventModel = CreateValidEventModel();
        var providerId = Guid.NewGuid();
        var invoice = CreateValidInvoice(posData: $"providerId:{providerId},accountCredit:1");
        var provider = new Provider { Id = providerId, BillingEmail = "provider@example.com" };

        _bitPayClient.GetInvoice(eventModel.Data.Id).Returns(invoice);
        _transactionRepository.GetByGatewayIdAsync(GatewayType.BitPay, invoice.Id).Returns((Transaction)null);
        _providerRepository.GetByIdAsync(providerId).Returns(Task.FromResult(provider));
        _paymentService.CreditAccountAsync(provider, Arg.Any<decimal>()).Returns(true);

        var result = await controller.PostIpn(eventModel, _validWebhookKey);

        Assert.IsType<OkResult>(result);
        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.ProviderId == providerId &&
            t.Type == TransactionType.Credit &&
            t.Gateway == GatewayType.BitPay &&
            t.PaymentMethodType == PaymentMethodType.BitPay));
        await _providerRepository.Received(1).ReplaceAsync(provider);
        await _mailService.Received(1).SendAddedCreditAsync("provider@example.com", 100.00m);
    }

    [Fact]
    public void GetIdsFromPosData_ValidOrganizationId_ReturnsCorrectId()
    {
        var controller = CreateController();
        var organizationId = Guid.NewGuid();
        var invoice = CreateValidInvoice(posData: $"organizationId:{organizationId},accountCredit:1");

        var result = controller.GetIdsFromPosData(invoice);

        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Null(result.UserId);
        Assert.Null(result.ProviderId);
    }

    [Fact]
    public void GetIdsFromPosData_ValidUserId_ReturnsCorrectId()
    {
        var controller = CreateController();
        var userId = Guid.NewGuid();
        var invoice = CreateValidInvoice(posData: $"userId:{userId},accountCredit:1");

        var result = controller.GetIdsFromPosData(invoice);

        Assert.Null(result.OrganizationId);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.ProviderId);
    }

    [Fact]
    public void GetIdsFromPosData_ValidProviderId_ReturnsCorrectId()
    {
        var controller = CreateController();
        var providerId = Guid.NewGuid();
        var invoice = CreateValidInvoice(posData: $"providerId:{providerId},accountCredit:1");

        var result = controller.GetIdsFromPosData(invoice);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.UserId);
        Assert.Equal(providerId, result.ProviderId);
    }

    [Fact]
    public void GetIdsFromPosData_InvalidGuid_ReturnsNull()
    {
        var controller = CreateController();
        var invoice = CreateValidInvoice(posData: "organizationId:invalid-guid,accountCredit:1");

        var result = controller.GetIdsFromPosData(invoice);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.UserId);
        Assert.Null(result.ProviderId);
    }

    [Fact]
    public void GetIdsFromPosData_NullPosData_ReturnsNull()
    {
        var controller = CreateController();
        var invoice = CreateValidInvoice(posData: null!);

        var result = controller.GetIdsFromPosData(invoice);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.UserId);
        Assert.Null(result.ProviderId);
    }

    [Fact]
    public void GetIdsFromPosData_EmptyPosData_ReturnsNull()
    {
        var controller = CreateController();
        var invoice = CreateValidInvoice(posData: "");

        var result = controller.GetIdsFromPosData(invoice);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.UserId);
        Assert.Null(result.ProviderId);
    }

    private static BitPayEventModel CreateValidEventModel(string invoiceId = "test-invoice-id")
    {
        return new BitPayEventModel
        {
            Event = new BitPayEventModel.EventModel { Code = 1005, Name = "invoice_confirmed" },
            Data = new BitPayEventModel.InvoiceDataModel { Id = invoiceId }
        };
    }

    private static Invoice CreateValidInvoice(string invoiceId = "test-invoice-id", string status = "complete",
        string currency = "USD", decimal price = 100.00m,
        string posData = "organizationId:550e8400-e29b-41d4-a716-446655440000,accountCredit:1")
    {
        return new Invoice
        {
            Id = invoiceId,
            Status = status,
            Currency = currency,
            Price = (double)price,
            PosData = posData,
            CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Transactions =
            [
                new InvoiceTransaction
                {
                    Type = null,
                    Confirmations = "1",
                    ReceivedTime = DateTime.UtcNow.ToString("O")
                }
            ]
        };
    }

}
