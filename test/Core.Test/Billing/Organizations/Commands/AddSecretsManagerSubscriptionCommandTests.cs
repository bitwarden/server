using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using StaticStorePlan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Test.Billing.Organizations.Commands;

public class AddSecretsManagerSubscriptionCommandTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly AddSecretsManagerSubscriptionCommand _command;

    public AddSecretsManagerSubscriptionCommandTests()
    {
        _command = new AddSecretsManagerSubscriptionCommand(
            Substitute.For<ILogger<AddSecretsManagerSubscriptionCommand>>(),
            _organizationService,
            _providerRepository,
            _pricingClient,
            _updateOrganizationSubscriptionCommand);
    }

    [Fact]
    public async Task Run_ReturnsBadRequest_WhenOrganizationAlreadyHasSecretsManager()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        organization.UseSecretsManager = true;

        var result = await _command.Run(organization, 5, 10);

        Assert.True(result.IsT1);
        Assert.Contains("Organization already uses Secrets Manager", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_ReturnsBadRequest_WhenPlanDoesNotSupportSecretsManager()
    {
        var organization = CreateOrganization(PlanType.FamiliesAnnually);
        var plan = MockPlans.Get(PlanType.FamiliesAnnually); // Families plan has no SecretsManager
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var result = await _command.Run(organization, 0, 0);

        Assert.True(result.IsT1);
        Assert.Contains("Invalid Secrets Manager plan selected", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_ReturnsBadRequest_WhenNoPaymentMethodAndNonFreePlan()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually, gatewayCustomerId: null);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        var result = await _command.Run(organization, 5, 10);

        Assert.True(result.IsT1);
        Assert.Contains("No payment method found", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_ReturnsBadRequest_WhenNoSubscriptionAndNonFreePlan()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually, gatewaySubscriptionId: null);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        var result = await _command.Run(organization, 5, 10);

        Assert.True(result.IsT1);
        Assert.Contains("No subscription found", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_ReturnsBadRequest_WhenOrganizationIsManagedByMSP()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));
        _providerRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider { Type = ProviderType.Msp });

        var result = await _command.Run(organization, 5, 10);

        Assert.True(result.IsT1);
        Assert.Contains("Managed Service Provider", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_DoesNotReturnBadRequest_WhenPlanIsDisabled()
    {
        // A disabled plan must not block an existing subscriber from adding SM.
        // The old command delegated to ValidateSecretsManagerPlan which internally called
        // ValidatePlan — that method threw when plan.Disabled was true. The new command
        // intentionally omits the Disabled check.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var disabledPlan = new DisabledEnterprisePlan();
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(disabledPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, 5, 10);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task Run_CallsUpdateSubscriptionCommand_WithCorrectChangeSet_WhenNonFreePlan()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        SetupSubscriptionCommandSuccess();

        await _command.Run(organization, additionalSmSeats: 5, additionalServiceAccounts: 10);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count == 2 &&
                cs.ChargeImmediately &&
                cs.Changes.Any(c => c.IsT0 && c.AsT0.PriceId == plan.SecretsManager.StripeSeatPlanId && c.AsT0.Quantity == 5) &&
                cs.Changes.Any(c => c.IsT0 && c.AsT0.PriceId == plan.SecretsManager.StripeServiceAccountPlanId && c.AsT0.Quantity == 10)));
    }

    [Fact]
    public async Task Run_SkipsUpdateSubscriptionCommand_WhenFreePlan()
    {
        var organization = CreateOrganization(PlanType.Free, gatewayCustomerId: null, gatewaySubscriptionId: null, seats: 2);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.Free));

        var result = await _command.Run(organization, additionalSmSeats: 0, additionalServiceAccounts: 0);

        Assert.True(result.IsT0);
        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
    }

    [Fact]
    public async Task Run_SkipsUpdateSubscriptionCommand_WhenBothAdditionsAreZero_NonFreePlan()
    {
        // Uses a custom plan with SM BaseSeats > 0 so that 0 additional seats passes the
        // "you have no SM seats" validation and we reach the empty-change-set guard.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = new EnterprisePlanWithSmBaseSeats();
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var result = await _command.Run(organization, additionalSmSeats: 0, additionalServiceAccounts: 0);

        Assert.True(result.IsT0);
        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
    }

    [Fact]
    public async Task Run_ReturnsBadRequest_WhenUpdateSubscriptionCommandFails()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        BillingCommandResult<Subscription> failure = new BadRequest("Stripe failure");
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(failure);

        var result = await _command.Run(organization, additionalSmSeats: 5, additionalServiceAccounts: 10);

        Assert.True(result.IsT1);
        Assert.Equal("Stripe failure", result.AsT1.Response);
        await _organizationService.DidNotReceiveWithAnyArgs().ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task Run_UpdatesOrganizationAndCache_OnSuccess()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, additionalSmSeats: 5, additionalServiceAccounts: 10);

        Assert.True(result.IsT0);
        Assert.Equal(plan.SecretsManager.BaseSeats + 5, organization.SmSeats);
        Assert.Equal(plan.SecretsManager.BaseServiceAccount + 10, organization.SmServiceAccounts);
        Assert.True(organization.UseSecretsManager);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    private void SetupSubscriptionCommandSuccess()
    {
        BillingCommandResult<Subscription> successResult = new Subscription();
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(successResult);
    }

    private static Organization CreateOrganization(
        PlanType planType,
        string? gatewayCustomerId = "cus_test123",
        string? gatewaySubscriptionId = "sub_test123",
        int? seats = 10) => new()
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId,
            UseSecretsManager = false,
            Seats = seats
        };

    /// <summary>
    /// Represents a plan that is disabled (no longer available for purchase) but still used
    /// by existing subscribers. Used to verify that the disabled-plan check does not block
    /// existing organizations from adding Secrets Manager.
    /// </summary>
    private sealed record DisabledEnterprisePlan : EnterprisePlan
    {
        public DisabledEnterprisePlan() : base(isAnnual: true)
        {
            Disabled = true;
        }
    }

    /// <summary>
    /// A non-free plan with SM BaseSeats > 0 to exercise the empty-change-set guard:
    /// when both additionalSmSeats and additionalServiceAccounts are 0, we must not
    /// call <see cref="IUpdateOrganizationSubscriptionCommand.Run"/> with an empty set.
    /// </summary>
    private sealed record EnterprisePlanWithSmBaseSeats : StaticStorePlan
    {
        public EnterprisePlanWithSmBaseSeats()
        {
            Type = PlanType.EnterpriseAnnually;
            ProductTier = ProductTierType.Enterprise;
            Name = "Enterprise (Annually)";
            NameLocalizationKey = "planNameEnterprise";
            DescriptionLocalizationKey = "planDescEnterprise";
            PasswordManager = new PasswordManagerPlanFeatures
            {
                BaseSeats = 10,
                HasAdditionalSeatsOption = true,
                StripeSeatPlanId = "2023-enterprise-org-seat-annually"
            };
            SecretsManager = new SecretsManagerPlanFeatures
            {
                BaseSeats = 5,
                BaseServiceAccount = 50,
                HasAdditionalSeatsOption = true,
                HasAdditionalServiceAccountOption = true,
                StripeSeatPlanId = "secrets-manager-enterprise-seat-annually",
                StripeServiceAccountPlanId = "secrets-manager-service-account-annually"
            };
        }
    }
}
