using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using NSubstitute;
using Stripe;
using Xunit;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Invoicing.Test;

public class GetOrganizationPlanChangePreviewQueryTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly GetOrganizationPlanChangePreviewQuery _sut;

    public GetOrganizationPlanChangePreviewQueryTests() =>
        _sut = new GetOrganizationPlanChangePreviewQuery(
            new RecordingLogger<GetOrganizationPlanChangePreviewQuery>(),
            _pricingClient, _stripeAdapter, _invoicePreviewService);

    [Fact]
    public async Task Run_PaidOrganization_SwapsEveryLineByIdProratesAndTaxesFromAddress()
    {
        var organization = new Organization
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
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Enterprise, Cadence = PlanCadenceType.Annually, Country = "US", PostalCode = "90210" };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(FullSubscription());

        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, planChange);

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
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.FamiliesAnnually,
            Seats = 6
        };
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Teams, Cadence = PlanCadenceType.Annually, Country = "US", PostalCode = "90210" };

        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(FamiliesPlan());
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(FamiliesSubscription());

        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, planChange);

        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Teams, PlanCadenceType.Annually);
        Assert.NotNull(options);
        AssertSwap(options!.SubscriptionDetails.Items, id: "si_families", price: "price_teams_seat", quantity: 6);
    }

    [Fact]
    public async Task Run_PackagedTeams2019OverIncludedSeats_CollapsesBaseAndDropsOverage()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually2019,
            Seats = 8
        };
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Enterprise, Cadence = PlanCadenceType.Annually, Country = "US", PostalCode = "90210" };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2019).Returns(Teams2019Plan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(Teams2019Subscription());

        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, planChange);

        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Enterprise, PlanCadenceType.Annually);
        Assert.NotNull(options);
        var items = options!.SubscriptionDetails.Items;
        AssertSwap(items, id: "si_base", price: "price_ent_seat", quantity: 8);
        AssertDeleted(items, id: "si_overage");
    }

    [Fact]
    public async Task Run_OrganizationWithoutSubscriptionOrCustomer_BuildsFullPricePreviewFromAddress()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            PlanType = PlanType.Free,
            Seats = 7,
            UseSecretsManager = false
        };
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Teams, Cadence = PlanCadenceType.Annually, Country = "US", PostalCode = "90210" };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());

        InvoiceCreatePreviewOptions? options = null;
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Do<InvoiceCreatePreviewOptions>(o => options = o),
                Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization, planChange);

        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Teams, PlanCadenceType.Annually);
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
    public async Task Run_PaidOrganizationMissingCurrentPlanLine_ThrowsBadRequest()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = "sub_1",
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Enterprise, Cadence = PlanCadenceType.Annually, Country = "US", PostalCode = "90210" };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());
        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(
            Subscription.FromJson("""{ "id": "sub_1", "items": { "data": [ { "id": "si_other", "quantity": 5, "price": { "id": "price_unrelated" } } ] } }"""));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, planChange));
    }

    [Fact]
    public async Task Run_BlankBillingAddress_ThrowsBadRequest()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            PlanType = PlanType.Free,
            Seats = 5
        };
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Teams, Cadence = PlanCadenceType.Annually, Country = "", PostalCode = "" };

        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(TeamsPlan());

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, planChange));
    }

    [Fact]
    public async Task Run_NoSubscriptionOnNonFreePlan_ThrowsBadRequestWithoutCallingStripe()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = null,
            PlanType = PlanType.TeamsAnnually,
            Seats = 5
        };
        var planChange = new OrganizationPlanChange { Tier = PlanTierType.Enterprise, Cadence = PlanCadenceType.Annually, Country = "US", PostalCode = "90210" };

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(EnterprisePlan());

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, planChange));
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

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

    private static Subscription Teams2019Subscription() => Subscription.FromJson("""
        {
          "id": "sub_1", "status": "active",
          "items": { "data": [
            { "id": "si_base", "quantity": 1, "price": { "id": "price_t2019_base" } },
            { "id": "si_overage", "quantity": 3, "price": { "id": "price_t2019_seat" } }
          ] }
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
        }
    }
}
