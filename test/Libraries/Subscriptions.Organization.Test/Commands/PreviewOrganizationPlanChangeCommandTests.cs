using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
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
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class PreviewOrganizationPlanChangeCommandTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly PreviewOrganizationPlanChangeCommand _sut;

    public PreviewOrganizationPlanChangeCommandTests() =>
        _sut = new PreviewOrganizationPlanChangeCommand(
            NullLogger<PreviewOrganizationPlanChangeCommand>.Instance,
            _pricingClient, _stripeAdapter, _taxService, _invoicePreviewService);

    [Fact]
    public async Task Run_PaidOrganization_SwapsEveryLineByIdProratesAndTaxesFromAddress()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5,
            UseSecretsManager = true,
            MaxStorageGb = 2,
            SmServiceAccounts = 2
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(FullSubscription());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually));

        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Enterprise, PlanCadenceType.Annually);
        Assert.NotNull(options);
        Assert.Equal("cus_1", options!.Customer);
        Assert.Equal("sub_1", options.Subscription);
        Assert.Equal(StripeConstants.ProrationBehavior.AlwaysInvoice, options.SubscriptionDetails.ProrationBehavior);
        Assert.True(options.AutomaticTax.Enabled);
        Assert.Equal("US", options.CustomerDetails.Address.Country);
        Assert.Equal("90210", options.CustomerDetails.Address.PostalCode);

        var items = options.SubscriptionDetails.Items;
        AssertSwap(items, id: "si_pm", price: "price_ent_seat", quantity: 5);
        AssertSwap(items, id: "si_storage", price: "price_ent_storage", quantity: 3);
        AssertSwap(items, id: "si_sm", price: "price_ent_sm_seat", quantity: 5);
        AssertSwap(items, id: "si_sa", price: "price_ent_sm_sa", quantity: 2);
    }

    [Fact]
    public async Task Run_FlatCurrentPlan_RepricesAtCurrentPlanBaseSeats()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.FamiliesAnnually,
            Seats = 9
        };

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(FamiliesPlan());
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(FamiliesSubscription());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually));

        Assert.NotNull(options);
        // Families' included seats (6), not the org's Seats (9) — the flat->seat swap reprices at the current plan's base.
        AssertSwap(options!.SubscriptionDetails.Items, id: "si_families", price: "price_teams_seat", quantity: 6);
    }

    [Fact]
    public async Task Run_PackagedTeams2019OverIncludedSeats_CollapsesBaseAndDropsOverage()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually2019,
            Seats = 8
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2019).Returns(Teams2019Plan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(Teams2019Subscription());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.NotNull(options);
        var items = options!.SubscriptionDetails.Items;
        AssertSwap(items, id: "si_base", price: "price_ent_seat", quantity: 8);
        AssertDeleted(items, id: "si_overage");
    }

    [Fact]
    public async Task Run_FreeOrganization_BuildsFullPricePreviewFromAddress()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            PlanType = PlanType.Free,
            Seats = 7,
            UseSecretsManager = false
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually));

        Assert.NotNull(options);
        Assert.Null(options!.Customer);
        Assert.Null(options.Subscription);
        Assert.Null(options.SubscriptionDetails.ProrationBehavior);
        Assert.Equal("US", options.CustomerDetails.Address.Country);
        var item = Assert.Single(options.SubscriptionDetails.Items);
        Assert.Equal("price_teams_seat", item.Price);
        Assert.Equal(7, item.Quantity);
        Assert.Null(item.Id);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_FreeOrganizationWithSecretsManager_AddsSecretsManagerSeatLine()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.Free,
            Seats = 3,
            UseSecretsManager = true,
            SmSeats = 2
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually));

        Assert.NotNull(options);
        var items = options!.SubscriptionDetails.Items;
        Assert.Contains(items, item => item.Price == "price_teams_seat" && item.Quantity == 3);
        Assert.Contains(items, item => item.Price == "price_teams_sm_seat" && item.Quantity == 2);
    }

    [Fact]
    public async Task Run_FreeOrganizationToFamilies_UsesFlatPriceAtQuantityOne()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(FamiliesPlan());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Families, PlanCadenceType.Annually));

        Assert.NotNull(options);
        var item = Assert.Single(options!.SubscriptionDetails.Items);
        Assert.Equal("price_families_flat", item.Price);
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public async Task Run_SecretsManagerOrganizationWithoutExtraServiceAccounts_HasNoServiceAccountLine()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5,
            UseSecretsManager = true,
            SmServiceAccounts = 0
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(SecretsManagerNoServiceAccountsSubscription());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.NotNull(options);
        var items = options!.SubscriptionDetails.Items;
        AssertSwap(items, id: "si_sm", price: "price_ent_sm_seat", quantity: 5);
        Assert.DoesNotContain(items, item => item.Price == "price_ent_sm_sa");
    }

    [Theory]
    [InlineData(PlanTierType.Teams, PlanCadenceType.Monthly, PlanType.TeamsMonthly)]
    [InlineData(PlanTierType.Teams, PlanCadenceType.Annually, PlanType.TeamsAnnually)]
    [InlineData(PlanTierType.Enterprise, PlanCadenceType.Monthly, PlanType.EnterpriseMonthly)]
    [InlineData(PlanTierType.Enterprise, PlanCadenceType.Annually, PlanType.EnterpriseAnnually)]
    public async Task Run_ResolvesPlanTypeFromTierAndCadence(PlanTierType tier, PlanCadenceType cadence, PlanType expectedPlanType)
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(expectedPlanType).Returns(tier == PlanTierType.Teams ? TeamsPlan() : EnterprisePlan());
        _invoicePreviewService.GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), tier, cadence).Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(tier, cadence));

        await _pricingClient.Received(1).GetPlanOrThrow(expectedPlanType);
    }

    [Fact]
    public async Task Run_RequestTaxId_SetsCustomerDetailsTaxIds()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _taxService.GetStripeTaxCode("DE", "DE123456789").Returns("eu_vat");
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually,
            country: "DE", postalCode: "10115", taxId: new TaxID("eu_vat", "DE123456789")));

        Assert.NotNull(options);
        var taxId = Assert.Single(options!.CustomerDetails.TaxIds);
        Assert.Equal("eu_vat", taxId.Type);
        Assert.Equal("DE123456789", taxId.Value);
    }

    [Fact]
    public async Task Run_NoRequestTaxId_FallsBackToCustomerOnFileTaxId()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(SubscriptionWithCustomerTaxId());
        _taxService.GetStripeTaxCode("US", "DE123456789").Returns("eu_vat");
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.NotNull(options);
        var taxId = Assert.Single(options!.CustomerDetails.TaxIds);
        Assert.Equal("eu_vat", taxId.Type);
        Assert.Equal("DE123456789", taxId.Value);
    }

    [Fact]
    public async Task Run_SpanishNifTaxId_AlsoAddsEuVat()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _taxService.GetStripeTaxCode("ES", "A12345678").Returns(StripeConstants.TaxIdType.SpanishNIF);
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually,
            country: "ES", postalCode: "28001", taxId: new TaxID(StripeConstants.TaxIdType.SpanishNIF, "A12345678")));

        Assert.NotNull(options);
        var taxIds = options!.CustomerDetails.TaxIds;
        Assert.Contains(taxIds, taxId => taxId.Type == StripeConstants.TaxIdType.SpanishNIF && taxId.Value == "A12345678");
        Assert.Contains(taxIds, taxId => taxId.Type == StripeConstants.TaxIdType.EUVAT && taxId.Value == "ESA12345678");
    }

    [Fact]
    public async Task Run_MissingBillingAddress_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };
        var request = new PreviewOrganizationPlanChangeRequest
        {
            Tier = PlanTierType.Teams,
            Cadence = PlanCadenceType.Annually,
            BillingAddress = null
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, request));
    }

    [Theory]
    [InlineData("", "90210")]
    [InlineData("USA", "90210")]
    [InlineData("US", "")]
    public async Task Run_InvalidBillingAddress_ThrowsBadRequest(string country, string postalCode)
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually, country, postalCode)));
    }

    [Fact]
    public async Task Run_UnsupportedTier_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Premium, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_FamiliesMonthly_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Families, PlanCadenceType.Monthly)));
    }

    [Fact]
    public async Task Run_SecretsManagerOrganizationTargetsPlanWithoutSecretsManager_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5, UseSecretsManager = true };

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(FamiliesPlan());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Families, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_SameTierChange_ThrowsBadRequestWithoutCallingStripe()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsMonthly,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually)));
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_Downgrade_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(FamiliesPlan());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Families, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_NoSubscriptionOnNonFreePlan_ThrowsConflictWithoutCallingStripe()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = null,
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually)));
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_FreeOrganizationMissingSeats_ThrowsConflict()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = null };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_PaidOrganizationMissingCurrentPlanLine_ThrowsConflict()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(
            Subscription.FromJson("""{ "id": "sub_1", "status": "active", "items": { "data": [ { "id": "si_other", "quantity": 5, "price": { "id": "price_unrelated" } } ] } }"""));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_SubscriptionMissingInStripe_ThrowsConflict()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = StripeConstants.ErrorCodes.ResourceMissing } });

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_SubscriptionInNonPreviewableStatus_ThrowsConflict()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(
            Subscription.FromJson("""{ "id": "sub_1", "status": "canceled", "items": { "data": [ { "id": "si_pm", "quantity": 5, "price": { "id": "price_teams_seat" } } ] } }"""));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually)));
    }

    [Fact]
    public async Task Run_StripeRejectsTaxLocation_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns<InvoicePreview>(_ => throw new StripeException { StripeError = new StripeError { Code = StripeConstants.ErrorCodes.CustomerTaxLocationInvalid } });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually, country: "ZZ", postalCode: "00000")));
    }

    [Fact]
    public async Task Run_StripeRejectsTaxId_ThrowsBadRequest()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns<InvoicePreview>(_ => throw new StripeException { StripeError = new StripeError { Code = StripeConstants.ErrorCodes.TaxIdInvalid } });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually,
                taxId: new TaxID("gb_vat", "invalid"))));
    }

    [Fact]
    public async Task Run_TrialingOrganization_EndsTrialInPreviewSoAmountReflectsPostTrialCharge()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(TrialingSubscription());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.NotNull(options);
        Assert.Equal("now", options!.SubscriptionDetails.TrialEnd?.ToString());
    }

    [Fact]
    public async Task Run_ActiveOrganization_DoesNotEndTrialInPreview()
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(FullSubscription());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.NotNull(options);
        Assert.Null(options!.SubscriptionDetails.TrialEnd);
    }

    [Fact]
    public async Task Run_TaxIdWithBlankValue_SendsNoTaxId()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PlanType = PlanType.Free, Seats = 5 };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, Request(PlanTierType.Teams, PlanCadenceType.Annually, taxId: new TaxID("us_ein", "")));

        Assert.NotNull(options);
        Assert.Null(options!.CustomerDetails.TaxIds);
    }

    private static PreviewOrganizationPlanChangeRequest Request(
        PlanTierType tier, PlanCadenceType cadence, string country = "US", string postalCode = "90210", TaxID? taxId = null) =>
        new()
        {
            Tier = tier,
            Cadence = cadence,
            BillingAddress = new BillingAddress { Country = country, PostalCode = postalCode, TaxId = taxId }
        };

    private static void AssertSwap(List<InvoiceSubscriptionDetailsItemOptions> items, string id, string price, long quantity)
    {
        var item = Assert.Single(items, candidate => candidate.Id == id);
        Assert.Equal(price, item.Price);
        Assert.Equal(quantity, item.Quantity);
        Assert.Null(item.Deleted);
    }

    private static void AssertDeleted(List<InvoiceSubscriptionDetailsItemOptions> items, string id)
    {
        var item = Assert.Single(items, candidate => candidate.Id == id);
        Assert.True(item.Deleted);
    }

    private static Subscription FullSubscription() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "active",
          "items": { "data": [
            { "id": "si_pm", "quantity": 5, "price": { "id": "price_teams_seat" } },
            { "id": "si_storage", "quantity": 3, "price": { "id": "price_teams_storage" } },
            { "id": "si_sm", "quantity": 5, "price": { "id": "price_teams_sm_seat" } },
            { "id": "si_sa", "quantity": 2, "price": { "id": "price_teams_sm_sa" } }
          ] }
        }
        """);

    private static Subscription FamiliesSubscription() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "active",
          "items": { "data": [ { "id": "si_families", "quantity": 1, "price": { "id": "price_families_flat" } } ] }
        }
        """);

    private static Subscription TrialingSubscription() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "trialing",
          "items": { "data": [ { "id": "si_pm", "quantity": 5, "price": { "id": "price_teams_seat" } } ] }
        }
        """);

    private static Subscription Teams2019Subscription() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "active",
          "items": { "data": [
            { "id": "si_base", "quantity": 1, "price": { "id": "price_t2019_base" } },
            { "id": "si_overage", "quantity": 3, "price": { "id": "price_t2019_seat" } }
          ] }
        }
        """);

    private static Subscription SecretsManagerNoServiceAccountsSubscription() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "active",
          "items": { "data": [
            { "id": "si_pm", "quantity": 5, "price": { "id": "price_teams_seat" } },
            { "id": "si_sm", "quantity": 5, "price": { "id": "price_teams_sm_seat" } }
          ] }
        }
        """);

    private static Subscription SubscriptionWithCustomerTaxId() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "active",
          "customer": { "id": "cus_1", "tax_ids": { "data": [ { "type": "eu_vat", "value": "DE123456789" } ] } },
          "items": { "data": [ { "id": "si_pm", "quantity": 5, "price": { "id": "price_teams_seat" } } ] }
        }
        """);

    private static PlanFeatures TeamsPlan() => new TestPlan(ProductTierType.Teams, isAnnual: true,
        passwordManager: new PlanFeatures.PasswordManagerPlanFeatures { StripeSeatPlanId = "price_teams_seat", StripeStoragePlanId = "price_teams_storage", BaseStorageGb = 1 },
        secretsManager: new PlanFeatures.SecretsManagerPlanFeatures { StripeSeatPlanId = "price_teams_sm_seat", StripeServiceAccountPlanId = "price_teams_sm_sa", BaseServiceAccount = 0 });

    private static PlanFeatures EnterprisePlan() => new TestPlan(ProductTierType.Enterprise, isAnnual: true,
        passwordManager: new PlanFeatures.PasswordManagerPlanFeatures { StripeSeatPlanId = "price_ent_seat", StripeStoragePlanId = "price_ent_storage", BaseStorageGb = 1 },
        secretsManager: new PlanFeatures.SecretsManagerPlanFeatures { StripeSeatPlanId = "price_ent_sm_seat", StripeServiceAccountPlanId = "price_ent_sm_sa", BaseServiceAccount = 0 });

    private static PlanFeatures FamiliesPlan() => new TestPlan(ProductTierType.Families, isAnnual: true,
        passwordManager: new PlanFeatures.PasswordManagerPlanFeatures { StripePlanId = "price_families_flat", StripeStoragePlanId = "price_families_storage", BaseSeats = 6 });

    private static PlanFeatures Teams2019Plan() => new TestPlan(ProductTierType.Teams, isAnnual: true,
        passwordManager: new PlanFeatures.PasswordManagerPlanFeatures { StripePlanId = "price_t2019_base", StripeSeatPlanId = "price_t2019_seat", BaseSeats = 5 });

    private static InvoicePreview SampleInvoicePreview() => new()
    {
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 5, Cost = 100m }
        },
        Cadence = PlanCadenceType.Annually,
        PlanTier = PlanTierType.Enterprise,
        EstimatedTax = 0m,
        Total = 100m,
        AmountDue = 100m
    };

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(ProductTierType productTier, bool isAnnual,
            PasswordManagerPlanFeatures passwordManager, SecretsManagerPlanFeatures? secretsManager = null)
        {
            ProductTier = productTier;
            IsAnnual = isAnnual;
            PasswordManager = passwordManager;
            SecretsManager = secretsManager;
            UpgradeSortOrder = productTier switch
            {
                ProductTierType.Free => -1,
                ProductTierType.Families => 1,
                ProductTierType.TeamsStarter => 2,
                ProductTierType.Teams => 3,
                ProductTierType.Enterprise => 4,
                _ => 0
            };
        }
    }
}
