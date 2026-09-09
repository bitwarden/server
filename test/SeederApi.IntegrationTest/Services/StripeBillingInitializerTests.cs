using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Stripe;
using Xunit;
using OrganizationPlan = Bit.Core.Models.StaticStore.Plan;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.SeederApi.IntegrationTest.Services;

/// <summary>
/// Covers the fail-fast configuration gate and the signup back-computation in
/// <see cref="StripeBillingInitializer"/>. Nothing here reaches Stripe: the billing service is a capturing
/// stub, so the assertions are about the <see cref="OrganizationSale"/> the seeder would have submitted.
/// </summary>
public class StripeBillingInitializerTests
{
    private const string _testKey = "sk_test_abc123";
    private const string _pricingUri = "https://billingpricing.qa.bitwarden.pw";

    [Fact]
    public void ValidateConfiguration_MissingApiKey_Throws()
    {
        var initializer = Build(Settings(apiKey: null));

        var ex = Assert.Throws<InvalidOperationException>(
            () => initializer.ValidateConfiguration(PlanType.TeamsMonthly));

        Assert.Contains("no Stripe API key", ex.Message);
    }

    [Theory]
    [InlineData("sk_live_abc123")]
    [InlineData("rk_test_abc123")]
    [InlineData("pk_test_abc123")]
    public void ValidateConfiguration_NonTestApiKey_Throws(string apiKey)
    {
        var initializer = Build(Settings(apiKey: apiKey));

        var ex = Assert.Throws<InvalidOperationException>(
            () => initializer.ValidateConfiguration(PlanType.TeamsMonthly));

        Assert.Contains("sk_test_", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateConfiguration_MissingPricingUri_ThrowsAndHintsAtTheEnvironment(string? pricingUri)
    {
        var initializer = Build(Settings(pricingUri: pricingUri));

        var ex = Assert.Throws<InvalidOperationException>(
            () => initializer.ValidateConfiguration(PlanType.TeamsMonthly));

        Assert.Contains("pricingUri", ex.Message);
        Assert.Contains("ASPNETCORE_ENVIRONMENT=Development", ex.Message);
    }

    [Fact]
    public void ValidateConfiguration_SelfHosted_Throws()
    {
        // The Pricing Service is never called in self-hosted mode, so GetPlanOrThrow could only ever 404.
        // dev/secrets.json.example ships selfHosted: true, which makes this the likeliest misconfiguration.
        var initializer = Build(Settings(selfHosted: true));

        var ex = Assert.Throws<InvalidOperationException>(
            () => initializer.ValidateConfiguration(PlanType.TeamsMonthly));

        Assert.Contains("selfHosted", ex.Message);
    }

    [Fact]
    public void ValidateConfiguration_FreePlan_Throws()
    {
        var initializer = Build(Settings());

        var ex = Assert.Throws<InvalidOperationException>(
            () => initializer.ValidateConfiguration(PlanType.Free));

        Assert.Contains("Free plan", ex.Message);
    }

    [Theory]
    [InlineData(PlanType.TeamsMonthly)]
    [InlineData(PlanType.EnterpriseAnnually)]
    [InlineData(PlanType.FamiliesAnnually)]
    public void ValidateConfiguration_TestKeyAndPricingUriOnPaidPlan_Passes(PlanType planType)
    {
        Build(Settings()).ValidateConfiguration(planType);
    }

    [Fact]
    public void BuildSignup_UsesTheCanonicalStripeTestPaymentShape()
    {
        var signup = StripeBillingInitializer.BuildSignup(Org(seats: 10), Plan(), new StripeBillingOptions());

        Assert.Equal(PaymentMethodType.Card, signup.PaymentMethodType);
        Assert.Equal("pm_card_visa", signup.PaymentToken);
        Assert.Equal("Seeder", signup.InitiationPath);

        // OrganizationSale.From dereferences TaxInfo unguarded whenever PaymentMethodType is set.
        Assert.NotNull(signup.TaxInfo);
        Assert.Equal("US", signup.TaxInfo.BillingAddressCountry);
        Assert.Equal("43432", signup.TaxInfo.BillingAddressPostalCode);
    }

    [Theory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 1, 9)]
    [InlineData(1, 1, 0)]
    [InlineData(null, 1, 0)]
    public void BuildSignup_BackComputesAdditionalSeatsAndClampsAtZero(int? seats, int baseSeats, int expected)
    {
        var signup = StripeBillingInitializer.BuildSignup(
            Org(seats: seats), Plan(baseSeats: baseSeats), new StripeBillingOptions());

        Assert.Equal(expected, signup.AdditionalSeats);
    }

    [Theory]
    [InlineData(5, 1, 4)]
    [InlineData(1, 1, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(null, 1, 0)]
    public void BuildSignup_BackComputesAdditionalStorageAndClampsAtZero(
        int? maxStorageGb, int baseStorageGb, int expected)
    {
        var signup = StripeBillingInitializer.BuildSignup(
            Org(maxStorageGb: (short?)maxStorageGb),
            Plan(baseStorageGb: (short)baseStorageGb),
            new StripeBillingOptions());

        Assert.Equal(expected, signup.AdditionalStorageGb);
    }

    [Fact]
    public void BuildSignup_SmSeatsNull_ExcludesSecretsManager()
    {
        // Seeded Teams/Enterprise orgs carry UseSecretsManager = true with SmSeats NULL. Inventing a
        // quantity from NULL would bill for seats nobody asked for, so SM stays out of the subscription.
        var organization = Org(seats: 10);
        organization.UseSecretsManager = true;
        organization.SmSeats = null;

        var signup = StripeBillingInitializer.BuildSignup(organization, Plan(), new StripeBillingOptions());

        Assert.False(signup.UseSecretsManager);
        Assert.Null(signup.AdditionalSmSeats);
        Assert.Null(signup.AdditionalServiceAccounts);
    }

    [Fact]
    public void BuildSignup_SmSeatsSet_IncludesAndBackComputesSecretsManager()
    {
        var organization = Org(seats: 10);
        organization.SmSeats = 8;
        organization.SmServiceAccounts = 60;

        var signup = StripeBillingInitializer.BuildSignup(
            organization,
            Plan(smBaseSeats: 2, smBaseServiceAccounts: 50),
            new StripeBillingOptions());

        Assert.True(signup.UseSecretsManager);
        Assert.Equal(6, signup.AdditionalSmSeats);
        Assert.Equal(10, signup.AdditionalServiceAccounts);
    }

    [Fact]
    public void BuildSignup_SmSeatsSetOnAPlanWithoutSecretsManager_ExcludesSecretsManager()
    {
        var organization = Org(seats: 10);
        organization.SmSeats = 8;

        var signup = StripeBillingInitializer.BuildSignup(
            organization, Plan(withSecretsManager: false), new StripeBillingOptions());

        Assert.False(signup.UseSecretsManager);
        Assert.Null(signup.AdditionalSmSeats);
    }

    [Fact]
    public void BuildSignup_DefaultOptions_RequestsATrial()
    {
        var signup = StripeBillingInitializer.BuildSignup(Org(seats: 10), Plan(), new StripeBillingOptions());

        Assert.False(signup.SkipTrial);
        Assert.Equal(30, signup.TrialLength);
    }

    [Fact]
    public void BuildSignup_ExplicitTrialDays_FlowsThrough()
    {
        var signup = StripeBillingInitializer.BuildSignup(
            Org(seats: 10), Plan(), new StripeBillingOptions { TrialDays = 7 });

        Assert.False(signup.SkipTrial);
        Assert.Equal(7, signup.TrialLength);
    }

    [Fact]
    public void BuildSignup_SkipTrial_LeavesTrialLengthUnset()
    {
        // A TrialLength alongside SkipTrial would be ambiguous downstream: OrganizationBillingService reads
        // SkipTrial first, but leaving the length null keeps the sale unambiguous.
        var signup = StripeBillingInitializer.BuildSignup(
            Org(seats: 10), Plan(), new StripeBillingOptions { SkipTrial = true, TrialDays = 7 });

        Assert.True(signup.SkipTrial);
        Assert.Null(signup.TrialLength);
    }

    [Fact]
    public async Task InitializeOrganizationAsync_FinalizesASaleForTheSeededOrganizationAsync()
    {
        var billingService = new CapturingOrganizationBillingService();
        var organization = Org(seats: 10);
        var initializer = new StripeBillingInitializer(
            Settings(), billingService, new StubPricingClient(Plan(baseSeats: 1)));

        await initializer.InitializeOrganizationAsync(organization, new StripeBillingOptions { TrialDays = 14 });

        var sale = Assert.Single(billingService.Sales);
        Assert.Same(organization, sale.Organization);
        Assert.Equal(organization.PlanType, sale.SubscriptionSetup.PlanType);
        Assert.Equal(9, sale.SubscriptionSetup.PasswordManagerOptions.Seats);
        Assert.Equal(14, sale.SubscriptionSetup.TrialLength);
        Assert.False(sale.SubscriptionSetup.SkipTrial);
        Assert.Equal("Seeder", sale.SubscriptionSetup.InitiationPath);
        Assert.NotNull(sale.CustomerSetup);
        Assert.Null(sale.SubscriptionSetup.SecretsManagerOptions);
    }

    [Fact]
    public async Task InitializeOrganizationAsync_LooksUpThePlanOfTheSeededOrganizationAsync()
    {
        var pricingClient = new StubPricingClient(Plan());
        var organization = Org(seats: 10);
        organization.PlanType = PlanType.TeamsAnnually;

        await new StripeBillingInitializer(Settings(), new CapturingOrganizationBillingService(), pricingClient)
            .InitializeOrganizationAsync(organization, new StripeBillingOptions());

        Assert.Equal(PlanType.TeamsAnnually, Assert.Single(pricingClient.RequestedPlanTypes));
    }

    [Fact]
    public async Task InitializeOrganizationAsync_BillingServiceThrows_WrapsAsInvalidOperationExceptionAsync()
    {
        var stripeException = new StripeException("The card was declined.");
        var billingService = new CapturingOrganizationBillingService { ThrowOnFinalize = stripeException };
        var organization = Org(seats: 10);
        var initializer = new StripeBillingInitializer(Settings(), billingService, new StubPricingClient(Plan()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeOrganizationAsync(organization, new StripeBillingOptions()));

        Assert.Contains(organization.Id.ToString(), ex.Message);
        Assert.Contains("already committed", ex.Message);
        Assert.Contains("customer: <none>", ex.Message);
        Assert.Contains("subscription: <none>", ex.Message);
        Assert.Same(stripeException, ex.InnerException);
    }

    [Fact]
    public async Task InitializeOrganizationAsync_BillingServiceThrowsAfterCustomerCreated_MessageReportsTheRealStateAsync()
    {
        // A customer-then-subscription failure leaves a real GatewayCustomerId committed. The message must
        // report it instead of assuming the "no gateway IDs" shape, so a developer knows to cancel the
        // orphaned Stripe customer rather than assuming there's nothing to clean up.
        var stripeException = new StripeException("The subscription price is invalid.");
        var billingService = new CapturingOrganizationBillingService { ThrowOnFinalize = stripeException };
        var organization = Org(seats: 10);
        organization.GatewayCustomerId = "cus_partial";
        var initializer = new StripeBillingInitializer(Settings(), billingService, new StubPricingClient(Plan()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeOrganizationAsync(organization, new StripeBillingOptions()));

        Assert.Contains("customer: cus_partial", ex.Message);
        Assert.Contains("subscription: <none>", ex.Message);
    }

    private static StripeBillingInitializer Build(GlobalSettings globalSettings) =>
        new(globalSettings, new CapturingOrganizationBillingService(), new StubPricingClient(Plan()));

    private static GlobalSettings Settings(
        string? apiKey = _testKey,
        string? pricingUri = _pricingUri,
        bool selfHosted = false)
    {
        var settings = new GlobalSettings { SelfHosted = selfHosted, PricingUri = pricingUri! };
        settings.Stripe.ApiKey = apiKey!;
        return settings;
    }

    private static Organization Org(int? seats = null, short? maxStorageGb = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Billing Test",
        BillingEmail = "owner@billingtest.example",
        PlanType = PlanType.TeamsMonthly,
        Seats = seats,
        MaxStorageGb = maxStorageGb,
    };

    private static OrganizationPlan Plan(
        int baseSeats = 0,
        short baseStorageGb = 0,
        int smBaseSeats = 0,
        short smBaseServiceAccounts = 0,
        bool withSecretsManager = true) =>
        new StubPlan(baseSeats, baseStorageGb, smBaseSeats, smBaseServiceAccounts, withSecretsManager);

    /// <summary>
    /// Minimal concrete <see cref="OrganizationPlan"/>. Its feature records use <c>protected init</c>, so a
    /// derived type is the only way to build one outside <c>Bit.Core</c>.
    /// </summary>
    private sealed record StubPlan : OrganizationPlan
    {
        internal StubPlan(
            int baseSeats,
            short baseStorageGb,
            int smBaseSeats,
            short smBaseServiceAccounts,
            bool withSecretsManager)
        {
            Type = PlanType.TeamsMonthly;
            ProductTier = ProductTierType.Teams;
            Name = "Stub";
            TrialPeriodDays = 7;
            PasswordManager = new StubPasswordManager(baseSeats, baseStorageGb);
            SecretsManager = withSecretsManager
                ? new StubSecretsManager(smBaseSeats, smBaseServiceAccounts)
                : null;
        }

        private sealed record StubPasswordManager : PasswordManagerPlanFeatures
        {
            internal StubPasswordManager(int baseSeats, short baseStorageGb)
            {
                BaseSeats = baseSeats;
                BaseStorageGb = baseStorageGb;
            }
        }

        private sealed record StubSecretsManager : SecretsManagerPlanFeatures
        {
            internal StubSecretsManager(int baseSeats, short baseServiceAccounts)
            {
                BaseSeats = baseSeats;
                BaseServiceAccount = baseServiceAccounts;
            }
        }
    }

    private sealed class CapturingOrganizationBillingService : IOrganizationBillingService
    {
        internal List<OrganizationSale> Sales { get; } = [];

        internal Exception? ThrowOnFinalize { get; set; }

        public Task Finalize(OrganizationSale sale)
        {
            if (ThrowOnFinalize is not null)
            {
                throw ThrowOnFinalize;
            }

            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task UpdateSubscriptionPlanFrequency(Organization organization, PlanType newPlanType) =>
            throw new NotSupportedException();

        public Task UpdateOrganizationNameAndEmail(Organization organization) =>
            throw new NotSupportedException();
    }

    private sealed class StubPricingClient(OrganizationPlan plan) : IPricingClient
    {
        internal List<PlanType> RequestedPlanTypes { get; } = [];

        public Task<OrganizationPlan?> GetPlan(PlanType planType) => throw new NotSupportedException();

        public Task<OrganizationPlan> GetPlanOrThrow(PlanType planType)
        {
            RequestedPlanTypes.Add(planType);
            return Task.FromResult(plan);
        }

        public Task<List<OrganizationPlan>> ListPlans() => throw new NotSupportedException();

        public Task<PremiumPlan> GetAvailablePremiumPlan() => throw new NotSupportedException();

        public Task<List<PremiumPlan>> ListPremiumPlans() => throw new NotSupportedException();
    }
}
