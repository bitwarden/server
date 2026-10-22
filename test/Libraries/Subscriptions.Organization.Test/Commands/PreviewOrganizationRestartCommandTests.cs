using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class PreviewOrganizationRestartCommandTests
{
    private const string NotEndedMessage = "Cannot restart a subscription that has not ended.";

    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IGetOrganizationRestartQuery _getOrganizationRestartQuery = Substitute.For<IGetOrganizationRestartQuery>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly PreviewOrganizationRestartCommand _sut;

    public PreviewOrganizationRestartCommandTests() =>
        _sut = new PreviewOrganizationRestartCommand(
            NullLogger<PreviewOrganizationRestartCommand>.Instance,
            _subscriberService, _getOrganizationRestartQuery, _taxService, _invoicePreviewService);

    [Fact]
    public async Task Run_CanceledSubscription_PreviewsRestartItemsAgainstCustomerWithoutProration()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        var subscription = new Subscription { Status = StripeConstants.SubscriptionStatus.Canceled, CustomerId = "cus_1" };
        _subscriberService.GetSubscription(organization).Returns(subscription);
        _getOrganizationRestartQuery.Run(organization, subscription).Returns(
            Restart(PlanType.TeamsAnnually, ProductTierType.Teams, true, ("price_seat", 5), ("price_storage", 2)));
        var options = CaptureOptions();

        await _sut.Run(organization, Request());

        Assert.NotNull(options.Value);
        Assert.Equal("cus_1", options.Value!.Customer);
        Assert.Null(options.Value.Subscription);
        Assert.Null(options.Value.Currency);
        Assert.Null(options.Value.SubscriptionDetails.ProrationBehavior);
        Assert.Equal(StripeConstants.BillingMode.Classic, options.Value.SubscriptionDetails.BillingMode.Type);
        Assert.True(options.Value.AutomaticTax.Enabled);
        Assert.Equal("US", options.Value.CustomerDetails.Address.Country);
        Assert.Equal("90210", options.Value.CustomerDetails.Address.PostalCode);
        Assert.Equal(
            [("price_seat", 5L), ("price_storage", 2L)],
            options.Value.SubscriptionDetails.Items.Select(item => (item.Price, item.Quantity!.Value)));
    }

    [Fact]
    public async Task Run_IncompleteExpiredSubscription_PreviewsRestartItemsAgainstCustomer()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        var subscription = new Subscription { Status = StripeConstants.SubscriptionStatus.IncompleteExpired, CustomerId = "cus_1" };
        _subscriberService.GetSubscription(organization).Returns(subscription);
        _getOrganizationRestartQuery.Run(organization, subscription).Returns(
            Restart(PlanType.TeamsAnnually, ProductTierType.Teams, true, ("price_seat", 5), ("price_storage", 2)));
        var options = CaptureOptions();

        await _sut.Run(organization, Request());

        Assert.NotNull(options.Value);
        Assert.Equal("cus_1", options.Value!.Customer);
        Assert.Equal(
            [("price_seat", 5L), ("price_storage", 2L)],
            options.Value.SubscriptionDetails.Items.Select(item => (item.Price, item.Quantity!.Value)));
    }

    [Fact]
    public async Task Run_NoTaxIdInRequest_LeavesTaxIdsNull()
    {
        var organization = ArrangeCanceledOrganization();
        var options = CaptureOptions();

        await _sut.Run(organization, Request());

        Assert.NotNull(options.Value);
        Assert.Null(options.Value!.CustomerDetails.TaxIds);
    }

    [Fact]
    public async Task Run_TaxIdInRequest_SendsDerivedTaxId()
    {
        var organization = ArrangeCanceledOrganization();
        _taxService.GetStripeTaxCode("DE", "DE123").Returns("eu_vat");
        var options = CaptureOptions();

        await _sut.Run(organization, Request(country: "DE", postalCode: "10115", taxId: new TaxIdSelection("eu_vat", "DE123")));

        Assert.NotNull(options.Value);
        var taxId = Assert.Single(options.Value!.CustomerDetails.TaxIds);
        Assert.Equal("eu_vat", taxId.Type);
        Assert.Equal("DE123", taxId.Value);
    }

    [Theory]
    [InlineData(StripeConstants.SubscriptionStatus.Active)]
    [InlineData(StripeConstants.SubscriptionStatus.PastDue)]
    [InlineData(StripeConstants.SubscriptionStatus.Unpaid)]
    [InlineData(StripeConstants.SubscriptionStatus.Trialing)]
    [InlineData(StripeConstants.SubscriptionStatus.Incomplete)]
    public async Task Run_SubscriptionNotEnded_ThrowsBadRequest(string status)
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        _subscriberService.GetSubscription(organization)
            .Returns(new Subscription { Status = status, CustomerId = "cus_1" });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, Request()));

        Assert.Equal(NotEndedMessage, exception.Message);
        await _getOrganizationRestartQuery.DidNotReceiveWithAnyArgs().Run(default!, default!);
        await _invoicePreviewService.DidNotReceiveWithAnyArgs()
            .GetInvoicePreviewAsync(default(InvoiceCreatePreviewOptions)!, default, default);
    }

    [Fact]
    public async Task Run_NoSubscription_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        _subscriberService.GetSubscription(organization).Returns((Subscription?)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, Request()));

        Assert.Equal(NotEndedMessage, exception.Message);
    }

    [Theory]
    [InlineData(PlanType.FamiliesAnnually, ProductTierType.Families, true, PlanTierType.Families, PlanCadenceType.Annually)]
    [InlineData(PlanType.TeamsMonthly, ProductTierType.Teams, false, PlanTierType.Teams, PlanCadenceType.Monthly)]
    [InlineData(PlanType.TeamsStarter, ProductTierType.TeamsStarter, false, PlanTierType.Teams, PlanCadenceType.Monthly)]
    [InlineData(PlanType.EnterpriseAnnually, ProductTierType.Enterprise, true, PlanTierType.Enterprise, PlanCadenceType.Annually)]
    public async Task Run_MapsPlanToTierAndCadence(
        PlanType planType, ProductTierType productTier, bool isAnnual, PlanTierType expectedTier, PlanCadenceType expectedCadence)
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        var subscription = new Subscription { Status = StripeConstants.SubscriptionStatus.Canceled, CustomerId = "cus_1" };
        _subscriberService.GetSubscription(organization).Returns(subscription);
        _getOrganizationRestartQuery.Run(organization, subscription)
            .Returns(Restart(planType, productTier, isAnnual, ("price_seat", 1)));
        CaptureOptions();

        await _sut.Run(organization, Request());

        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), expectedTier, expectedCadence);
    }

    [Fact]
    public async Task Run_MissingBillingAddress_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, new PreviewOrganizationRestartRequest { BillingAddress = null }));

        await _subscriberService.DidNotReceiveWithAnyArgs().GetSubscription(default!);
    }

    [Fact]
    public async Task Run_TaxIdMissingValue_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(taxId: new TaxIdSelection("eu_vat", " "))));

        await _subscriberService.DidNotReceiveWithAnyArgs().GetSubscription(default!);
    }

    [Fact]
    public async Task Run_StripeTaxLocationInvalid_ThrowsBadRequest()
    {
        var organization = ArrangeCanceledOrganization();
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns<InvoicePreview>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.CustomerTaxLocationInvalid }
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, Request()));

        Assert.Equal(
            "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.",
            exception.Message);
    }

    [Fact]
    public async Task Run_StripeTaxIdInvalid_ThrowsBadRequest()
    {
        var organization = ArrangeCanceledOrganization();
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns<InvoicePreview>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.TaxIdInvalid }
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, Request()));

        Assert.Equal(
            "Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.",
            exception.Message);
    }

    private OrganizationEntity ArrangeCanceledOrganization()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        var subscription = new Subscription { Status = StripeConstants.SubscriptionStatus.Canceled, CustomerId = "cus_1" };
        _subscriberService.GetSubscription(organization).Returns(subscription);
        _getOrganizationRestartQuery.Run(organization, subscription).Returns(
            Restart(PlanType.TeamsAnnually, ProductTierType.Teams, true, ("price_seat", 5)));
        return organization;
    }

    private Captured<InvoiceCreatePreviewOptions> CaptureOptions()
    {
        var captured = new Captured<InvoiceCreatePreviewOptions>();
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => captured.Value = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());
        return captured;
    }

    private static OrganizationRestart Restart(
        PlanType planType, ProductTierType productTier, bool isAnnual, params (string PriceId, long Quantity)[] items) =>
        new(new TestPlan(planType, productTier, isAnnual),
            items.Select(item => new OrganizationRestartItem(item.PriceId, item.Quantity)).ToList());

    private static PreviewOrganizationRestartRequest Request(
        string country = "US", string postalCode = "90210", TaxIdSelection? taxId = null) => new()
        {
            BillingAddress = new BillingAddressSelections(country, postalCode, taxId)
        };

    private static InvoicePreview SampleInvoicePreview() => new()
    {
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 10m }
        },
        Cadence = PlanCadenceType.Annually,
        PlanTier = PlanTierType.Teams,
        EstimatedTax = 0m,
        Total = 10m,
        AmountDue = 10m
    };

    private sealed class Captured<T> where T : class
    {
        public T? Value { get; set; }
    }

    private sealed record TestPlan : Plan
    {
        public TestPlan(PlanType type, ProductTierType productTier, bool isAnnual)
        {
            Type = type;
            ProductTier = productTier;
            IsAnnual = isAnnual;
        }
    }
}
