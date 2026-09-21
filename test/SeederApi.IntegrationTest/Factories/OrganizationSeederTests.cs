using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class OrganizationSeederTests
{
    private const string Name = "Acme Corp";
    private const string Domain = "acme.test";

    private static OrganizationSeed Seed() => new() { Name = Name, Domain = Domain, Seats = 10 };

    [Fact]
    public void Create_ManglesNameAndIdentifier()
    {
        var organization = OrganizationSeeder.Create(Seed(), new ManglerService());

        Assert.NotEqual(Name, organization.Name);
        Assert.Matches(@"^[a-f0-9]{8}-Acme Corp$", organization.Name);
        Assert.Matches(@"^[a-f0-9]{8}-acme\.test$", organization.Identifier);
    }

    [Fact]
    public void Create_AppliesPlanFeatures()
    {
        var free = OrganizationSeeder.Create(
            Seed() with { PlanType = PlanType.Free }, new NoOpManglerService());
        var enterprise = OrganizationSeeder.Create(
            Seed() with { PlanType = PlanType.EnterpriseAnnually }, new NoOpManglerService());

        Assert.Equal("Free", free.Plan);
        Assert.Equal(PlanType.Free, free.PlanType);
        Assert.Equal((short?)2, free.MaxCollections);
        Assert.False(free.UseGroups);

        Assert.Equal("Enterprise (Annually)", enterprise.Plan);
        Assert.Equal(PlanType.EnterpriseAnnually, enterprise.PlanType);
        Assert.True(enterprise.UseGroups);
    }

    [Fact]
    public void Create_OverridesLayerOnTopOfPlanDefaults()
    {
        // Free turns UseGroups off; the override must win, proving overrides run after PlanFeatures.Apply.
        var organization = OrganizationSeeder.Create(
            Seed() with
            {
                PlanType = PlanType.Free,
                Overrides = new OrganizationOverrides { UseGroups = true, UseSso = true }
            },
            new NoOpManglerService());

        Assert.True(organization.UseGroups);
        Assert.True(organization.UseSso);
        // Untouched override properties leave the plan value in place.
        Assert.False(organization.UseScim);
    }

    [Fact]
    public void Create_OverrideDisablesSecretsManagerOnEnterprise()
    {
        // Enterprise flags Secrets Manager on by default; the override is the way to turn it back off.
        var defaultOn = OrganizationSeeder.Create(
            Seed() with { PlanType = PlanType.EnterpriseAnnually }, new NoOpManglerService());
        Assert.True(defaultOn.UseSecretsManager);

        var overriddenOff = OrganizationSeeder.Create(
            Seed() with
            {
                PlanType = PlanType.EnterpriseAnnually,
                Overrides = new OrganizationOverrides { UseSecretsManager = false }
            },
            new NoOpManglerService());
        Assert.False(overriddenOff.UseSecretsManager);

        // Intended precedence, not a bug: the EnableSecretsManager gate runs after overrides and forces SM on.
        var gateWins = OrganizationSeeder.Create(
            Seed() with
            {
                PlanType = PlanType.EnterpriseAnnually,
                EnableSecretsManager = true,
                Overrides = new OrganizationOverrides { UseSecretsManager = false }
            },
            new NoOpManglerService());
        Assert.True(gateWins.UseSecretsManager);
    }

    [Fact]
    public void Create_SetsBillingGatewayIdentifiers()
    {
        var organization = OrganizationSeeder.Create(
            Seed() with
            {
                Gateway = GatewayType.Stripe,
                GatewayCustomerId = "cus_test123",
                GatewaySubscriptionId = "sub_test123"
            },
            new NoOpManglerService());

        Assert.Equal(GatewayType.Stripe, organization.Gateway);
        Assert.Equal("cus_test123", organization.GatewayCustomerId);
        Assert.Equal("sub_test123", organization.GatewaySubscriptionId);
    }

    [Fact]
    public void Create_WithoutGateway_LeavesBillingNull()
    {
        var organization = OrganizationSeeder.Create(Seed(), new NoOpManglerService());

        Assert.Null(organization.Gateway);
        Assert.Null(organization.GatewayCustomerId);
        Assert.Null(organization.GatewaySubscriptionId);
    }

    [Fact]
    public void Create_WithSecretsManager_SetsSeatsAndServiceAccounts()
    {
        var defaulted = OrganizationSeeder.Create(
            Seed() with { EnableSecretsManager = true }, new NoOpManglerService());

        Assert.True(defaulted.UseSecretsManager);
        Assert.Equal(10, defaulted.SmSeats);            // falls back to Seats
        Assert.Equal(50, defaulted.SmServiceAccounts);  // Enterprise base allotment

        var explicitSeats = OrganizationSeeder.Create(
            Seed() with { EnableSecretsManager = true, SmSeats = 3, SmServiceAccounts = 7 },
            new NoOpManglerService());

        Assert.Equal(3, explicitSeats.SmSeats);
        Assert.Equal(7, explicitSeats.SmServiceAccounts);
    }

    [Fact]
    public void Create_WithoutSecretsManager_LeavesSeatsUnprovisioned()
    {
        // The Enterprise plan already flags UseSecretsManager on; EnableSecretsManager is what
        // provisions the subscription seats. Without it, SmSeats/SmServiceAccounts stay null
        // and the supplied values are ignored.
        var enterprise = OrganizationSeeder.Create(
            Seed() with { SmSeats = 3, SmServiceAccounts = 7 }, new NoOpManglerService());

        Assert.True(enterprise.UseSecretsManager);
        Assert.Null(enterprise.SmSeats);
        Assert.Null(enterprise.SmServiceAccounts);

        // Free starts from the minimal feature set, so the flag is off too.
        var free = OrganizationSeeder.Create(
            Seed() with { PlanType = PlanType.Free }, new NoOpManglerService());

        Assert.False(free.UseSecretsManager);
    }

    [Fact]
    public void Create_FreeWithSecretsManager_SetsFreeTierDefaults()
    {
        var free = OrganizationSeeder.Create(
            Seed() with { PlanType = PlanType.Free, EnableSecretsManager = true },
            new NoOpManglerService());

        Assert.True(free.UseSecretsManager);
        Assert.Equal(2, free.SmSeats);            // Free tier base seats
        Assert.Equal(3, free.SmServiceAccounts);  // Free tier base service accounts
    }

    [Fact]
    public void Create_SecretsManagerOnUnsupportedPlan_Throws()
    {
        // Families has no Secrets Manager tier, so enabling it must still throw.
        var seed = Seed() with { PlanType = PlanType.FamiliesAnnually, EnableSecretsManager = true };

        Assert.Throws<ArgumentException>(() => OrganizationSeeder.Create(seed, new NoOpManglerService()));
    }

    [Fact]
    public void Create_KeysLandOnMatchingProperties()
    {
        // The swap this DTO exists to prevent: two adjacent string? params at every former call site.
        var organization = OrganizationSeeder.Create(
            Seed() with { PublicKey = "public-key-value", PrivateKey = "private-key-value" },
            new NoOpManglerService());

        Assert.Equal("public-key-value", organization.PublicKey);
        Assert.Equal("private-key-value", organization.PrivateKey);
    }
}
