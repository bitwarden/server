using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Models.BitStripe;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

public class PaymentHistoryServiceTests
{
    [Fact]
    public async Task GetInvoiceHistoryAsync_Succeeds()
    {
        // Arrange
        var subscriber = new Organization { GatewayCustomerId = "cus_id", GatewaySubscriptionId = "sub_id" };
        var invoices = new List<Invoice> { new() { Id = "in_id" } };
        var stripeAdapter = Substitute.For<IStripeAdapter>();
        stripeAdapter.InvoiceListAsync(Arg.Any<StripeInvoiceListOptions>()).Returns(invoices);
        var transactionRepository = Substitute.For<ITransactionRepository>();
        var logger = Substitute.For<ILogger<PaymentHistoryService>>();
        var paymentHistoryService = new PaymentHistoryService(stripeAdapter, transactionRepository, logger);

        // Act
        var result = await paymentHistoryService.GetInvoiceHistoryAsync(subscriber);

        // Assert
        Assert.NotEmpty(result);
        Assert.Single(result);
        await stripeAdapter.Received(1).InvoiceListAsync(Arg.Any<StripeInvoiceListOptions>());
    }

    [Fact]
    public async Task GetInvoiceHistoryAsync_SubscriberNull_ReturnsNull()
    {
        // Arrange
        var paymentHistoryService = new PaymentHistoryService(
            Substitute.For<IStripeAdapter>(),
            Substitute.For<ITransactionRepository>(),
            Substitute.For<ILogger<PaymentHistoryService>>());

        // Act
        var result = await paymentHistoryService.GetInvoiceHistoryAsync(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_Succeeds()
    {
        // Arrange
        var subscriber = new Organization { Id = Guid.NewGuid() };
        var transactions = new List<Transaction> { new() { Id = Guid.NewGuid() } };
        var transactionRepository = Substitute.For<ITransactionRepository>();
        transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id, Arg.Any<int>(), Arg.Any<DateTime?>()).Returns(transactions);
        var stripeAdapter = Substitute.For<IStripeAdapter>();
        var logger = Substitute.For<ILogger<PaymentHistoryService>>();
        var paymentHistoryService = new PaymentHistoryService(stripeAdapter, transactionRepository, logger);

        // Act
        var result = await paymentHistoryService.GetTransactionHistoryAsync(subscriber);

        // Assert
        Assert.NotEmpty(result);
        Assert.Single(result);
        await transactionRepository.Received(1).GetManyByOrganizationIdAsync(subscriber.Id, Arg.Any<int>(), Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_SubscriberNull_ReturnsNull()
    {
        // Arrange
        var paymentHistoryService = new PaymentHistoryService(
            Substitute.For<IStripeAdapter>(),
            Substitute.For<ITransactionRepository>(),
            Substitute.For<ILogger<PaymentHistoryService>>());

        // Act
        var result = await paymentHistoryService.GetTransactionHistoryAsync(null);

        // Assert
        Assert.Empty(result);
    }
}
