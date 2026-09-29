using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Models.Requests;
using Bit.Subscriptions.User.Queries;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test.Queries;

public class GetSubscriptionPurchasePreviewQueryTests
{
    private const string PremiumSeatPriceId = "premium-annually-2026";
    private const string PremiumStoragePriceId = "personal-storage-gb-annually";

    private readonly RecordingLogger<GetSubscriptionPurchasePreviewQuery> _logger = new();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly ISubscriptionDiscountService _subscriptionDiscountService = Substitute.For<ISubscriptionDiscountService>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly GetSubscriptionPurchasePreviewQuery _sut;

    public GetSubscriptionPurchasePreviewQueryTests()
    {
        _sut = new GetSubscriptionPurchasePreviewQuery(_logger, _pricingClient, _subscriptionDiscountService, _invoicePreviewService);
        _pricingClient.GetAvailablePremiumPlan().Returns(new PremiumPlan
        {
            Seat = new PremiumPurchasable { StripePriceId = PremiumSeatPriceId },
            Storage = new PremiumPurchasable { StripePriceId = PremiumStoragePriceId }
        });
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)100)]
    public async Task Run_WhenAdditionalStorageIsOutOfRange_ThrowsBadRequestWithModelState(short additionalStorage)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(User(), Request(additionalStorage: additionalStorage)));

        Assert.True(exception.ModelState!.ContainsKey(nameof(GetSubscriptionPurchasePreviewRequest.AdditionalStorage)));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(null, "12345", nameof(GetSubscriptionPurchasePreviewRequest.Country))]
    [InlineData("", "12345", nameof(GetSubscriptionPurchasePreviewRequest.Country))]
    [InlineData("  ", "12345", nameof(GetSubscriptionPurchasePreviewRequest.Country))]
    [InlineData("USA", "12345", nameof(GetSubscriptionPurchasePreviewRequest.Country))]
    [InlineData("US", null, nameof(GetSubscriptionPurchasePreviewRequest.PostalCode))]
    [InlineData("US", "", nameof(GetSubscriptionPurchasePreviewRequest.PostalCode))]
    [InlineData("US", "   ", nameof(GetSubscriptionPurchasePreviewRequest.PostalCode))]
    public async Task Run_WhenBillingAddressIsMalformed_ThrowsBadRequestWithModelState(
        string? country, string? postalCode, string expectedKey)
    {
        var request = new GetSubscriptionPurchasePreviewRequest(0, null, country, postalCode);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey(expectedKey));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData((short)0)]
    public async Task Run_WithNoAdditionalStorage_PreviewsOnlyThePremiumSeat(short? additionalStorage)
    {
        ArrangePreview();

        await _sut.Run(User(), Request(additionalStorage: additionalStorage));

        var item = Assert.Single(CapturedOptions().SubscriptionDetails.Items);
        Assert.Equal(PremiumSeatPriceId, item.Price);
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public async Task Run_WithAdditionalStorage_AddsTheStorageItem()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(additionalStorage: 5));

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Equal(2, items.Count);
        Assert.Single(items, item => item.Price == PremiumSeatPriceId && item.Quantity == 1);
        Assert.Single(items, item => item.Price == PremiumStoragePriceId && item.Quantity == 5);
    }

    [Fact]
    public async Task Run_BuildsPurchasePreviewOptionsWithoutACustomerOrSubscription()
    {
        ArrangePreview();

        await _sut.Run(User(), Request());

        var options = CapturedOptions();
        Assert.True(options.AutomaticTax.Enabled);
        Assert.Equal("usd", options.Currency);
        Assert.Equal("classic", options.SubscriptionDetails.BillingMode.Type);
        Assert.Null(options.Customer);
        Assert.Null(options.Subscription);
        Assert.Null(options.Expand);
        Assert.Equal("US", options.CustomerDetails.Address.Country);
        Assert.Equal("12345", options.CustomerDetails.Address.PostalCode);
    }

    [Theory]
    [MemberData(nameof(BlankCoupons))]
    public async Task Run_WithNoUsableCoupons_DoesNotCheckEligibility(string[]? coupons)
    {
        ArrangePreview();

        await _sut.Run(User(), Request(coupons: coupons));

        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs()
            .ValidateDiscountEligibilityForUserAsync(default!, default!, default);
        Assert.Null(CapturedOptions().Discounts);
    }

    public static TheoryData<string[]?> BlankCoupons() => new() { null, Array.Empty<string>(), new[] { "", "  " } };

    [Fact]
    public async Task Run_WithEligibleCoupons_TrimsAndAppliesThem()
    {
        var user = User();
        _subscriptionDiscountService
            .ValidateDiscountEligibilityForUserAsync(user, Arg.Any<IReadOnlyList<string>>(), DiscountTierType.Premium)
            .Returns(true);
        ArrangePreview();

        await _sut.Run(user, Request(coupons: [" A ", ""]));

        await _subscriptionDiscountService.Received(1).ValidateDiscountEligibilityForUserAsync(
            user, Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "A" })), DiscountTierType.Premium);
        var discount = Assert.Single(CapturedOptions().Discounts);
        Assert.Equal("A", discount.Coupon);
    }

    [Fact]
    public async Task Run_WithIneligibleCoupons_DropsThemAndStillPreviews()
    {
        _subscriptionDiscountService
            .ValidateDiscountEligibilityForUserAsync(Arg.Any<UserEntity>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<DiscountTierType>())
            .Returns(false);
        var preview = ArrangePreview();

        var result = await _sut.Run(User(), Request(coupons: ["A", "B"]));

        Assert.Same(preview, result);
        Assert.Null(CapturedOptions().Discounts);
    }

    [Fact]
    public async Task Run_PreviewsAnnualPremiumAndReturnsTheServiceResult()
    {
        var preview = ArrangePreview();

        var result = await _sut.Run(User(), Request());

        Assert.Same(preview, result);
        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Premium, PlanCadenceType.Annually);
    }

    [Fact]
    public async Task Run_WhenStripeRejectsTheTaxLocation_ThrowsBadRequest()
    {
        ArrangePreviewFailure("customer_tax_location_invalid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), Request()));

        Assert.Contains("Your location wasn't recognized", exception.Message);
    }

    [Fact]
    public async Task Run_WhenStripeFailsForAnotherReason_PropagatesTheStripeException()
    {
        ArrangePreviewFailure("api_error");

        await Assert.ThrowsAsync<StripeException>(() => _sut.Run(User(), Request()));
    }

    [Fact]
    public async Task Run_WhenThePreviewResolvesNoSeatLine_ThrowsConflictAndLogsTheFault()
    {
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(PreviewWithoutSeats());

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(User(), Request(additionalStorage: 2)));

        Assert.Equal("The plan could not be previewed. Please contact support for assistance.", exception.Message);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains(PremiumSeatPriceId, error);
    }

    private static InvoicePreview PreviewWithoutSeats() => new()
    {
        PlanTier = PlanTierType.Premium,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems(),
        EstimatedTax = 1.76m,
        Total = 21.56m,
        AmountDue = 21.56m
    };

    private static UserEntity User() => new() { Id = Guid.NewGuid() };

    private static GetSubscriptionPurchasePreviewRequest Request(short? additionalStorage = 0, string[]? coupons = null) =>
        new(additionalStorage, coupons, "US", "12345");

    private async Task AssertNoIoAsync()
    {
        await _pricingClient.DidNotReceive().GetAvailablePremiumPlan();
        await _invoicePreviewService.DidNotReceiveWithAnyArgs()
            .GetInvoicePreviewAsync(default(InvoiceCreatePreviewOptions)!, default, default);
    }

    private InvoicePreview ArrangePreview()
    {
        var preview = new InvoicePreview
        {
            PlanTier = PlanTierType.Premium,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new PasswordManagerInvoiceItems
            {
                Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 19.8m }
            },
            EstimatedTax = 1.76m,
            Total = 21.56m,
            AmountDue = 21.56m
        };
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(preview);
        return preview;
    }

    private void ArrangePreviewFailure(string code) =>
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns<InvoicePreview>(_ => throw new StripeException { StripeError = new StripeError { Code = code } });

    private InvoiceCreatePreviewOptions CapturedOptions() =>
        (InvoiceCreatePreviewOptions)_invoicePreviewService.ReceivedCalls().Single().GetArguments()[0]!;
}
