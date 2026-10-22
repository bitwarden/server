using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Exceptions;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Test.Billing.Mocks.Plans;
using NSubstitute;
using Stripe;
using Xunit;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Test.Billing.Subscriptions;

public class GetOrganizationRestartQueryTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly GetOrganizationRestartQuery _query;

    public GetOrganizationRestartQueryTests()
    {
        _query = new GetOrganizationRestartQuery(_pricingClient);
    }

    [Fact]
    public async Task Run_PlanNotFound_ThrowsConflict()
    {
        var organization = new Organization { PlanType = PlanType.EnterpriseAnnually };
        _pricingClient.ListPlans().Returns([MockPlans.Get(PlanType.TeamsAnnually)]);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _query.Run(organization, CanceledSubscription(("some-price-id", 10))));

        Assert.Equal("Could not find plan for organization's plan type", exception.Message);
    }

    [Fact]
    public async Task Run_DisabledPlanWithNoEnabledReplacement_ThrowsConflict()
    {
        var organization = new Organization { PlanType = PlanType.TeamsAnnually2020 };
        var oldPlan = new DisabledTeams2020Plan();
        _pricingClient.ListPlans().Returns([oldPlan]);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _query.Run(
            organization, CanceledSubscription((oldPlan.PasswordManager.StripeSeatPlanId, 10))));

        Assert.Equal("Could not find the current, enabled plan for organization's tier and cadence", exception.Message);
    }

    [Fact]
    public async Task Run_MissingPasswordManagerItem_ThrowsConflict()
    {
        var organization = new Organization { PlanType = PlanType.EnterpriseAnnually };
        _pricingClient.ListPlans().Returns([MockPlans.Get(PlanType.EnterpriseAnnually)]);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _query.Run(organization, CanceledSubscription(("some-other-price-id", 10))));

        Assert.Equal("Organization's subscription does not have a Password Manager subscription item.", exception.Message);
    }

    [Fact]
    public async Task Run_EnabledPlan_ReturnsSamePlanAndSeatItem()
    {
        var organization = new Organization { PlanType = PlanType.EnterpriseAnnually };
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.ListPlans().Returns([plan]);

        var restart = await _query.Run(organization, CanceledSubscription((plan.PasswordManager.StripeSeatPlanId, 10)));

        Assert.Equal(PlanType.EnterpriseAnnually, restart.Plan.Type);
        Assert.Equal([new OrganizationRestartItem(plan.PasswordManager.StripeSeatPlanId, 10)], restart.Items);
    }

    [Fact]
    public async Task Run_PackagedPlan_UsesStripePlanId()
    {
        var organization = new Organization { PlanType = PlanType.FamiliesAnnually };
        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        _pricingClient.ListPlans().Returns([plan]);

        var restart = await _query.Run(organization, CanceledSubscription((plan.PasswordManager.StripePlanId, 1)));

        Assert.Equal([new OrganizationRestartItem(plan.PasswordManager.StripePlanId, 1)], restart.Items);
    }

    [Fact]
    public async Task Run_WithStorage_MapsStorageItem()
    {
        var organization = new Organization { PlanType = PlanType.TeamsAnnually };
        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        _pricingClient.ListPlans().Returns([plan]);

        var restart = await _query.Run(organization, CanceledSubscription(
            (plan.PasswordManager.StripeSeatPlanId, 5),
            (plan.PasswordManager.StripeStoragePlanId, 3)));

        Assert.Contains(new OrganizationRestartItem(plan.PasswordManager.StripeStoragePlanId, 3), restart.Items);
    }

    [Fact]
    public async Task Run_WithSecretsManager_MapsSeatAndServiceAccountItems()
    {
        var organization = new Organization { PlanType = PlanType.EnterpriseMonthly };
        var plan = MockPlans.Get(PlanType.EnterpriseMonthly);
        _pricingClient.ListPlans().Returns([plan]);

        var restart = await _query.Run(organization, CanceledSubscription(
            (plan.PasswordManager.StripeSeatPlanId, 15),
            (plan.SecretsManager.StripeSeatPlanId, 5),
            (plan.SecretsManager.StripeServiceAccountPlanId, 50)));

        Assert.Contains(new OrganizationRestartItem(plan.SecretsManager.StripeSeatPlanId, 5), restart.Items);
        Assert.Contains(new OrganizationRestartItem(plan.SecretsManager.StripeServiceAccountPlanId, 50), restart.Items);
    }

    [Fact]
    public async Task Run_DisabledPlan_ReturnsEnabledReplacementWithRemappedPrices()
    {
        var organization = new Organization { PlanType = PlanType.TeamsAnnually2020 };
        var oldPlan = new DisabledTeams2020Plan();
        var newPlan = MockPlans.Get(PlanType.TeamsAnnually);
        _pricingClient.ListPlans().Returns([oldPlan, newPlan]);

        var restart = await _query.Run(organization, CanceledSubscription(
            (oldPlan.PasswordManager.StripeSeatPlanId, 10),
            (oldPlan.PasswordManager.StripeStoragePlanId, 2),
            (oldPlan.SecretsManager.StripeSeatPlanId, 4),
            (oldPlan.SecretsManager.StripeServiceAccountPlanId, 60)));

        Assert.Equal(PlanType.TeamsAnnually, restart.Plan.Type);
        Assert.Equal(
            [
                new OrganizationRestartItem(newPlan.PasswordManager.StripeSeatPlanId, 10),
                new OrganizationRestartItem(newPlan.PasswordManager.StripeStoragePlanId, 2),
                new OrganizationRestartItem(newPlan.SecretsManager.StripeSeatPlanId, 4),
                new OrganizationRestartItem(newPlan.SecretsManager.StripeServiceAccountPlanId, 60)
            ],
            restart.Items);
    }

    [Fact]
    public async Task Run_NewPlanWithoutSecretsManager_DropsSecretsManagerItems()
    {
        var organization = new Organization { PlanType = PlanType.TeamsAnnually2020 };
        var oldPlan = new DisabledTeams2020Plan();
        var newPlan = new TeamsPlanWithoutSecretsManager();
        _pricingClient.ListPlans().Returns([oldPlan, newPlan]);

        var restart = await _query.Run(organization, CanceledSubscription(
            (oldPlan.PasswordManager.StripeSeatPlanId, 10),
            (oldPlan.SecretsManager.StripeSeatPlanId, 4),
            (oldPlan.SecretsManager.StripeServiceAccountPlanId, 60)));

        Assert.Equal([new OrganizationRestartItem(newPlan.PasswordManager.StripeSeatPlanId, 10)], restart.Items);
    }

    private static Subscription CanceledSubscription(params (string PriceId, long Quantity)[] items) => new()
    {
        Status = "canceled",
        CustomerId = "cus_123",
        Items = new StripeList<SubscriptionItem>
        {
            Data = items
                .Select(item => new SubscriptionItem { Price = new Price { Id = item.PriceId }, Quantity = item.Quantity })
                .ToList()
        }
    };

    private record DisabledTeams2020Plan : Teams2020Plan
    {
        public DisabledTeams2020Plan() : base(true)
        {
            Disabled = true;
        }
    }

    private record TeamsPlanWithoutSecretsManager : Plan
    {
        public TeamsPlanWithoutSecretsManager()
        {
            Type = PlanType.TeamsAnnually;
            ProductTier = ProductTierType.Teams;
            IsAnnual = true;
            PasswordManager = new PasswordManagerPlanFeatures
            {
                StripeSeatPlanId = "teams-no-sm-seat",
                StripeStoragePlanId = "teams-no-sm-storage"
            };
        }
    }
}
