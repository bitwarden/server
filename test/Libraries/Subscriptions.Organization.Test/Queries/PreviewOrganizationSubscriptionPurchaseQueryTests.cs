using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;
using NSubstitute;
using Stripe;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.Organization.Test.Queries;

public class PreviewOrganizationSubscriptionPurchaseQueryTests
{
    private const string TaxIdValue = "DE123456789";
    private const string CatalogFaultMessage = "The plan could not be previewed. Please contact support for assistance.";

    private readonly RecordingLogger<PreviewOrganizationSubscriptionPurchaseQuery> _logger = new();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly ISubscriptionDiscountService _subscriptionDiscountService = Substitute.For<ISubscriptionDiscountService>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly PreviewOrganizationSubscriptionPurchaseQuery _sut;

    public PreviewOrganizationSubscriptionPurchaseQueryTests()
    {
        _sut = new PreviewOrganizationSubscriptionPurchaseQuery(
            _logger, _pricingClient, _subscriptionDiscountService, _taxService, _invoicePreviewService);

        _pricingClient.GetPlan(PlanType.FamiliesAnnually).Returns(TestPlan.Packaged("2020-families-org-annually", "personal-storage-gb-annually"));
        _pricingClient.GetPlan(PlanType.TeamsAnnually).Returns(TestPlan.SeatBased("teams-seat-annually", "storage-gb-annually", "sm-teams-seat-annually", "sm-teams-sa-annually"));
        _pricingClient.GetPlan(PlanType.TeamsMonthly).Returns(TestPlan.SeatBased("teams-seat-monthly", "storage-gb-monthly", "sm-teams-seat-monthly", "sm-teams-sa-monthly"));
        _pricingClient.GetPlan(PlanType.EnterpriseAnnually).Returns(TestPlan.SeatBased("enterprise-seat-annually", "storage-gb-annually", "sm-enterprise-seat-annually", "sm-enterprise-sa-annually"));
        _pricingClient.GetPlan(PlanType.EnterpriseMonthly).Returns(TestPlan.SeatBased("enterprise-seat-monthly", "storage-gb-monthly", "sm-enterprise-seat-monthly", "sm-enterprise-sa-monthly"));
    }

    [Fact]
    public async Task Run_WhenPurchaseIsMissing_ThrowsBadRequest()
    {
        var request = new PreviewOrganizationSubscriptionPurchaseRequest(null, Address());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey("Purchase"));
        await AssertNoIoAsync();
    }

    [Fact]
    public async Task Run_WhenPasswordManagerIsMissing_ThrowsBadRequest()
    {
        var request = Request(Purchase(ProductTierType.Teams) with { PasswordManager = null });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey("Purchase.PasswordManager"));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(ProductTierType.Free)]
    [InlineData(ProductTierType.TeamsStarter)]
    [InlineData((ProductTierType)99)]
    public async Task Run_WhenTierIsNotPurchasable_ThrowsBadRequest(ProductTierType tier)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), Request(Purchase(tier))));

        Assert.True(exception.ModelState!.ContainsKey("Purchase.Tier"));
        await AssertNoIoAsync();
    }

    [Fact]
    public async Task Run_WhenCadenceIsUndefined_ThrowsBadRequest()
    {
        var request = Request(Purchase(ProductTierType.Teams, (PlanCadenceType)9));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey("Purchase.Cadence"));
        await AssertNoIoAsync();
    }

    [Fact]
    public async Task Run_WhenFamiliesIsMonthly_ThrowsBadRequest()
    {
        var request = Request(Purchase(ProductTierType.Families, PlanCadenceType.Monthly));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.Equal("Monthly cadence is not available on the Families plan.", ErrorFor(exception, "Purchase.Cadence"));
        await AssertNoIoAsync();
    }

    [Fact]
    public async Task Run_WhenFamiliesIncludesSecretsManager_ThrowsBadRequest()
    {
        var request = Request(Purchase(ProductTierType.Families) with { SecretsManager = new SecretsManagerSelections(1, 0, false) });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.Equal("Secrets Manager is not available on the Families plan.", ErrorFor(exception, "Purchase.SecretsManager"));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(ProductTierType.Teams)]
    [InlineData(ProductTierType.Enterprise)]
    public async Task Run_WhenSponsoredOnANonFamiliesTier_ThrowsBadRequest(ProductTierType tier)
    {
        var request = Request(Purchase(tier, passwordManager: new PasswordManagerSelections(1, 0, true)));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey("Purchase.PasswordManager.Sponsored"));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(0, 0, "Purchase.PasswordManager.Seats", "Password Manager seats must be between 1 and 100,000")]
    [InlineData(100001, 0, "Purchase.PasswordManager.Seats", "Password Manager seats must be between 1 and 100,000")]
    [InlineData(1, -1, "Purchase.PasswordManager.AdditionalStorage", "Additional storage must be between 0 and 99 GB")]
    [InlineData(1, 100, "Purchase.PasswordManager.AdditionalStorage", "Additional storage must be between 0 and 99 GB")]
    public async Task Run_WhenPasswordManagerSelectionsAreOutOfRange_ThrowsBadRequest(
        int seats, int additionalStorage, string expectedKey, string expectedMessage)
    {
        var request = Request(Purchase(ProductTierType.Teams, passwordManager: new PasswordManagerSelections(seats, additionalStorage, false)));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.Equal(expectedMessage, ErrorFor(exception, expectedKey));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(0, 0, "Purchase.SecretsManager.Seats")]
    [InlineData(100001, 0, "Purchase.SecretsManager.Seats")]
    [InlineData(1, -1, "Purchase.SecretsManager.AdditionalServiceAccounts")]
    [InlineData(1, 100001, "Purchase.SecretsManager.AdditionalServiceAccounts")]
    public async Task Run_WhenSecretsManagerSelectionsAreOutOfRange_ThrowsBadRequest(
        int seats, int additionalServiceAccounts, string expectedKey)
    {
        var request = Request(Purchase(ProductTierType.Enterprise) with
        {
            SecretsManager = new SecretsManagerSelections(seats, additionalServiceAccounts, false)
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey(expectedKey));
        await AssertNoIoAsync();
    }

    [Fact]
    public async Task Run_WhenBillingAddressIsMissing_ThrowsBadRequest()
    {
        var request = new PreviewOrganizationSubscriptionPurchaseRequest(Purchase(ProductTierType.Teams), null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey("BillingAddress"));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(null, "12345", "BillingAddress.Country")]
    [InlineData("", "12345", "BillingAddress.Country")]
    [InlineData("  ", "12345", "BillingAddress.Country")]
    [InlineData("USA", "12345", "BillingAddress.Country")]
    [InlineData("US", null, "BillingAddress.PostalCode")]
    [InlineData("US", "", "BillingAddress.PostalCode")]
    [InlineData("US", "   ", "BillingAddress.PostalCode")]
    public async Task Run_WhenBillingAddressIsMalformed_ThrowsBadRequest(string? country, string? postalCode, string expectedKey)
    {
        var request = Request(Purchase(ProductTierType.Teams), new BillingAddressSelections(country, postalCode, null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey(expectedKey));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(null, TaxIdValue, "BillingAddress.TaxId.Code")]
    [InlineData(" ", TaxIdValue, "BillingAddress.TaxId.Code")]
    [InlineData("eu_vat", null, "BillingAddress.TaxId.Value")]
    [InlineData("eu_vat", "", "BillingAddress.TaxId.Value")]
    public async Task Run_WhenTaxIdIsIncomplete_ThrowsBadRequest(string? code, string? value, string expectedKey)
    {
        var request = Request(Purchase(ProductTierType.Enterprise), new BillingAddressSelections("DE", "10115", new TaxIdSelection(code, value)));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), request));

        Assert.True(exception.ModelState!.ContainsKey(expectedKey));
        await AssertNoIoAsync();
    }

    [Theory]
    [InlineData(ProductTierType.Families, PlanCadenceType.Annually, PlanType.FamiliesAnnually, PlanTierType.Families)]
    [InlineData(ProductTierType.Teams, PlanCadenceType.Annually, PlanType.TeamsAnnually, PlanTierType.Teams)]
    [InlineData(ProductTierType.Teams, PlanCadenceType.Monthly, PlanType.TeamsMonthly, PlanTierType.Teams)]
    [InlineData(ProductTierType.Enterprise, PlanCadenceType.Annually, PlanType.EnterpriseAnnually, PlanTierType.Enterprise)]
    [InlineData(ProductTierType.Enterprise, PlanCadenceType.Monthly, PlanType.EnterpriseMonthly, PlanTierType.Enterprise)]
    public async Task Run_ResolvesThePlanAndPreviewsItsTierAndCadence(
        ProductTierType tier, PlanCadenceType cadence, PlanType expectedPlanType, PlanTierType expectedTier)
    {
        var preview = ArrangePreview();

        var result = await _sut.Run(User(), Request(Purchase(tier, cadence)));

        Assert.Same(preview, result);
        await _pricingClient.Received(1).GetPlan(expectedPlanType);
        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), expectedTier, cadence);
    }

    [Fact]
    public async Task Run_WhenThePricingServiceHasNoPlan_ThrowsConflictAndLogsRatherThanNotFound()
    {
        _pricingClient.GetPlan(PlanType.TeamsAnnually).Returns((Bit.Core.Models.StaticStore.Plan?)null);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(User(), Request(Purchase(ProductTierType.Teams))));

        Assert.Equal(CatalogFaultMessage, exception.Message);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains("TeamsAnnually", error);
        await _invoicePreviewService.DidNotReceiveWithAnyArgs()
            .GetInvoicePreviewAsync(default(InvoiceCreatePreviewOptions)!, default, default);
    }

    [Fact]
    public async Task Run_BuildsPurchasePreviewOptionsWithoutACustomerOrSubscription()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Teams)));

        var options = CapturedOptions();
        Assert.True(options.AutomaticTax.Enabled);
        Assert.Equal("usd", options.Currency);
        Assert.Equal("classic", options.SubscriptionDetails.BillingMode.Type);
        Assert.Null(options.Customer);
        Assert.Null(options.Subscription);
        Assert.Null(options.Expand);
        Assert.Equal("US", options.CustomerDetails.Address.Country);
        Assert.Equal("12345", options.CustomerDetails.Address.PostalCode);
        Assert.Null(options.CustomerDetails.TaxIds);
    }

    [Fact]
    public async Task Run_ForTeamsWithoutStorage_PreviewsOnlyTheSeats()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Teams, passwordManager: new PasswordManagerSelections(10, 0, false))));

        var item = Assert.Single(CapturedOptions().SubscriptionDetails.Items);
        Assert.Equal("teams-seat-annually", item.Price);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public async Task Run_ForTeamsWithStorage_AddsTheStorageItem()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Teams, passwordManager: new PasswordManagerSelections(10, 3, false))));

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Equal(2, items.Count);
        Assert.Single(items, item => item.Price == "teams-seat-annually" && item.Quantity == 10);
        Assert.Single(items, item => item.Price == "storage-gb-annually" && item.Quantity == 3);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(10, 3)]
    public async Task Run_ForEnterpriseWithSecretsManager_AddsServiceAccountsOnlyWhenRequested(
        int additionalServiceAccounts, int expectedItemCount)
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Enterprise, PlanCadenceType.Monthly) with
        {
            SecretsManager = new SecretsManagerSelections(5, additionalServiceAccounts, false)
        }));

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Equal(expectedItemCount, items.Count);
        Assert.Single(items, item => item.Price == "enterprise-seat-monthly" && item.Quantity == 1);
        Assert.Single(items, item => item.Price == "sm-enterprise-seat-monthly" && item.Quantity == 5);
        Assert.Equal(additionalServiceAccounts > 0,
            items.Any(item => item.Price == "sm-enterprise-sa-monthly" && item.Quantity == additionalServiceAccounts));
    }

    [Fact]
    public async Task Run_ForPackagedFamilies_PreviewsOnePackageRegardlessOfSeats()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Families, passwordManager: new PasswordManagerSelections(6, 0, false))));

        var item = Assert.Single(CapturedOptions().SubscriptionDetails.Items);
        Assert.Equal("2020-families-org-annually", item.Price);
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public async Task Run_ForFamiliesWithEligibleCoupons_TrimsAndAppliesThem()
    {
        var user = User();
        _subscriptionDiscountService
            .ValidateDiscountEligibilityForUserAsync(user, Arg.Any<IReadOnlyList<string>>(), DiscountTierType.Families)
            .Returns(true);
        ArrangePreview();

        await _sut.Run(user, Request(Purchase(ProductTierType.Families) with { Coupons = [" A ", " "] }));

        await _subscriptionDiscountService.Received(1).ValidateDiscountEligibilityForUserAsync(
            user, Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "A" })), DiscountTierType.Families);
        var discount = Assert.Single(CapturedOptions().Discounts);
        Assert.Equal("A", discount.Coupon);
    }

    [Fact]
    public async Task Run_ForFamiliesWithIneligibleCoupons_DropsThemAndStillPreviews()
    {
        _subscriptionDiscountService
            .ValidateDiscountEligibilityForUserAsync(Arg.Any<UserEntity>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<DiscountTierType>())
            .Returns(false);
        var preview = ArrangePreview();

        var result = await _sut.Run(User(), Request(Purchase(ProductTierType.Families) with { Coupons = ["A"] }));

        Assert.Same(preview, result);
        Assert.Null(CapturedOptions().Discounts);
    }

    [Fact]
    public async Task Run_ForFamiliesWithBlankCoupons_DoesNotCheckEligibility()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Families) with { Coupons = ["", "  "] }));

        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs()
            .ValidateDiscountEligibilityForUserAsync(default!, default!, default);
        Assert.Null(CapturedOptions().Discounts);
    }

    [Theory]
    [InlineData(ProductTierType.Teams)]
    [InlineData(ProductTierType.Enterprise)]
    public async Task Run_ForBusinessTiers_IgnoresUserCoupons(ProductTierType tier)
    {
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(tier) with { Coupons = ["A"] }));

        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs()
            .ValidateDiscountEligibilityForUserAsync(default!, default!, default);
        Assert.Null(CapturedOptions().Discounts);
    }

    [Fact]
    public async Task Run_ForStandaloneSecretsManager_ForcesTheSystemCouponAndKeepsEveryAddOn()
    {
        ArrangePreview();

        await _sut.Run(User(), Request(new PurchaseSelections(
            ProductTierType.Teams,
            PlanCadenceType.Annually,
            new PasswordManagerSelections(4, 2, false),
            new SecretsManagerSelections(4, 20, true),
            ["USER-COUPON"])));

        var options = CapturedOptions();
        var discount = Assert.Single(options.Discounts);
        Assert.Equal("sm-standalone", discount.Coupon);
        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs()
            .ValidateDiscountEligibilityForUserAsync(default!, default!, default);

        var items = options.SubscriptionDetails.Items;
        Assert.Equal(4, items.Count);
        Assert.Single(items, item => item.Price == "teams-seat-annually" && item.Quantity == 4);
        Assert.Single(items, item => item.Price == "storage-gb-annually" && item.Quantity == 2);
        Assert.Single(items, item => item.Price == "sm-teams-seat-annually" && item.Quantity == 4);
        Assert.Single(items, item => item.Price == "sm-teams-sa-annually" && item.Quantity == 20);
    }

    [Fact]
    public async Task Run_ForSponsoredFamilies_SendsOnlyTheSponsoredPriceAndReturnsTheServiceResultUnchanged()
    {
        var sponsoredPreview = SponsoredPreview();
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Families, PlanCadenceType.Annually)
            .Returns(sponsoredPreview);

        var result = await _sut.Run(User(), Request(Purchase(ProductTierType.Families) with
        {
            PasswordManager = new PasswordManagerSelections(6, 0, true),
            Coupons = ["A"]
        }));

        Assert.Same(sponsoredPreview, result);
        var options = CapturedOptions();
        var item = Assert.Single(options.SubscriptionDetails.Items);
        Assert.Equal("2021-family-for-enterprise-annually", item.Price);
        Assert.Equal(1, item.Quantity);
        Assert.Null(options.Discounts);
        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs()
            .ValidateDiscountEligibilityForUserAsync(default!, default!, default);
        await _pricingClient.DidNotReceiveWithAnyArgs().GetPlan(default);
    }

    [Fact]
    public async Task Run_ForSponsoredFamiliesWithStorage_AddsTheFamiliesStorageItem()
    {
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Families, PlanCadenceType.Annually)
            .Returns(SponsoredPreview());

        await _sut.Run(User(), Request(Purchase(ProductTierType.Families) with
        {
            PasswordManager = new PasswordManagerSelections(1, 5, true)
        }));

        var options = CapturedOptions();
        Assert.Collection(options.SubscriptionDetails.Items,
            item =>
            {
                Assert.Equal("2021-family-for-enterprise-annually", item.Price);
                Assert.Equal(1, item.Quantity);
            },
            item =>
            {
                Assert.Equal("personal-storage-gb-annually", item.Price);
                Assert.Equal(5, item.Quantity);
            });
        Assert.Null(options.Discounts);
        await _pricingClient.Received(1).GetPlan(PlanType.FamiliesAnnually);
    }

    private static InvoicePreview SponsoredPreview() => new()
    {
        PlanTier = PlanTierType.Families,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 0m }
        },
        EstimatedTax = 0m,
        Total = 0m,
        AmountDue = 0m
    };

    [Fact]
    public async Task Run_WithATaxIdWhoseTypeCanBeDerived_SendsTheDerivedType()
    {
        _taxService.GetStripeTaxCode("DE", TaxIdValue).Returns("eu_vat");
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Enterprise),
            new BillingAddressSelections("DE", "10115", new TaxIdSelection("client_code", TaxIdValue))));

        var taxId = Assert.Single(CapturedOptions().CustomerDetails.TaxIds);
        Assert.Equal("eu_vat", taxId.Type);
        Assert.Equal(TaxIdValue, taxId.Value);
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public async Task Run_WithATaxIdWhoseTypeCannotBeDerived_FallsBackToTheClientTypeAndWarnsWithoutTheValue()
    {
        _taxService.GetStripeTaxCode(Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Enterprise),
            new BillingAddressSelections("DE", "10115", new TaxIdSelection("eu_vat", TaxIdValue))));

        var taxId = Assert.Single(CapturedOptions().CustomerDetails.TaxIds);
        Assert.Equal("eu_vat", taxId.Type);
        Assert.Equal(TaxIdValue, taxId.Value);
        var warning = Assert.Single(_logger.Warnings);
        Assert.Contains("DE", warning);
        Assert.Contains("eu_vat", warning);
        Assert.DoesNotContain(TaxIdValue, warning);
    }

    [Fact]
    public async Task Run_WithASpanishNif_AlsoSendsThePrefixedEuVat()
    {
        _taxService.GetStripeTaxCode("ES", "A12345678").Returns("es_cif");
        ArrangePreview();

        await _sut.Run(User(), Request(Purchase(ProductTierType.Enterprise),
            new BillingAddressSelections("ES", "28001", new TaxIdSelection("es_cif", "A12345678"))));

        var taxIds = CapturedOptions().CustomerDetails.TaxIds;
        Assert.Equal(2, taxIds.Count);
        Assert.Equal("es_cif", taxIds[0].Type);
        Assert.Equal("A12345678", taxIds[0].Value);
        Assert.Equal("eu_vat", taxIds[1].Type);
        Assert.Equal("ESA12345678", taxIds[1].Value);
    }

    [Fact]
    public async Task Run_WhenStripeRejectsTheTaxLocation_ThrowsBadRequest()
    {
        ArrangePreviewFailure("customer_tax_location_invalid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), Request(Purchase(ProductTierType.Teams))));

        Assert.Contains("Your location wasn't recognized", exception.Message);
    }

    [Fact]
    public async Task Run_WhenStripeRejectsTheTaxId_ThrowsBadRequest()
    {
        ArrangePreviewFailure("tax_id_invalid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(User(), Request(Purchase(ProductTierType.Teams))));

        Assert.Equal("The tax ID number you provided was invalid. Please try again or contact support for assistance.", exception.Message);
    }

    [Fact]
    public async Task Run_WhenStripeFailsForAnotherReason_PropagatesTheStripeException()
    {
        ArrangePreviewFailure("api_error");

        await Assert.ThrowsAsync<StripeException>(() => _sut.Run(User(), Request(Purchase(ProductTierType.Teams))));
    }

    [Fact]
    public async Task Run_WhenThePreviewResolvesNoSeatLine_ThrowsConflictAndLogsTheFaultWithoutTheTaxId()
    {
        _taxService.GetStripeTaxCode("DE", TaxIdValue).Returns("eu_vat");
        ArrangePreviewWithoutSeats(PlanTierType.Enterprise);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(User(), Request(
            Purchase(ProductTierType.Enterprise, passwordManager: new PasswordManagerSelections(5, 0, false)),
            new BillingAddressSelections("DE", "10115", new TaxIdSelection("eu_vat", TaxIdValue)))));

        Assert.Equal(CatalogFaultMessage, exception.Message);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains("enterprise-seat-annually", error);
        Assert.DoesNotContain(TaxIdValue, error);
    }

    [Fact]
    public async Task Run_WhenTheSponsoredPreviewResolvesNoSeatLine_ThrowsConflictAndLogsTheFault()
    {
        ArrangePreviewWithoutSeats(PlanTierType.Families);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(User(), Request(
            Purchase(ProductTierType.Families, passwordManager: new PasswordManagerSelections(1, 0, true)))));

        var error = Assert.Single(_logger.Errors);
        Assert.Contains("2021-family-for-enterprise-annually", error);
    }

    private void ArrangePreviewWithoutSeats(PlanTierType planTier) =>
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), planTier, Arg.Any<PlanCadenceType>())
            .Returns(new InvoicePreview
            {
                PlanTier = planTier,
                Cadence = PlanCadenceType.Annually,
                PasswordManager = new PasswordManagerInvoiceItems(),
                EstimatedTax = 0m,
                Total = 0m,
                AmountDue = 0m
            });

    private static UserEntity User() => new() { Id = Guid.NewGuid() };

    private static BillingAddressSelections Address() => new("US", "12345", null);

    private static PurchaseSelections Purchase(
        ProductTierType tier,
        PlanCadenceType cadence = PlanCadenceType.Annually,
        PasswordManagerSelections? passwordManager = null) =>
        new(tier, cadence, passwordManager ?? new PasswordManagerSelections(1, 0, false), null, null);

    private static PreviewOrganizationSubscriptionPurchaseRequest Request(PurchaseSelections purchase, BillingAddressSelections? billingAddress = null) =>
        new(purchase, billingAddress ?? Address());

    private static string? ErrorFor(BadRequestException exception, string key) =>
        exception.ModelState![key]?.Errors.Single().ErrorMessage;

    private async Task AssertNoIoAsync()
    {
        await _pricingClient.DidNotReceiveWithAnyArgs().GetPlan(default);
        await _invoicePreviewService.DidNotReceiveWithAnyArgs()
            .GetInvoicePreviewAsync(default(InvoiceCreatePreviewOptions)!, default, default);
        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs()
            .ValidateDiscountEligibilityForUserAsync(default!, default!, default);
    }

    private InvoicePreview ArrangePreview()
    {
        var preview = new InvoicePreview
        {
            PlanTier = PlanTierType.Teams,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new PasswordManagerInvoiceItems
            {
                Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 10, Cost = 48m }
            },
            EstimatedTax = 38.4m,
            Total = 518.4m,
            AmountDue = 518.4m
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

    private sealed record TestPlan : Bit.Core.Models.StaticStore.Plan
    {
        private TestPlan(PasswordManagerPlanFeatures passwordManager, SecretsManagerPlanFeatures? secretsManager)
        {
            PasswordManager = passwordManager;
            SecretsManager = secretsManager!;
        }

        public static TestPlan Packaged(string planId, string storagePlanId) =>
            new(new PasswordManagerPlanFeatures { StripePlanId = planId, StripeStoragePlanId = storagePlanId }, null);

        public static TestPlan SeatBased(string seatPlanId, string storagePlanId, string secretsManagerSeatPlanId, string serviceAccountPlanId) =>
            new(
                new PasswordManagerPlanFeatures { StripeSeatPlanId = seatPlanId, StripeStoragePlanId = storagePlanId },
                new SecretsManagerPlanFeatures { StripeSeatPlanId = secretsManagerSeatPlanId, StripeServiceAccountPlanId = serviceAccountPlanId });
    }
}
