using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Test.Billing.Mocks.Plans;
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
            _organizationService,
            _providerRepository,
            _pricingClient,
            _updateOrganizationSubscriptionCommand);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenOrganizationAlreadyHasSecretsManager()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        organization.UseSecretsManager = true;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, 5, 10));
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenPlanDoesNotSupportSecretsManager()
    {
        var organization = CreateOrganization(PlanType.FamiliesAnnually);
        var plan = MockPlans.Get(PlanType.FamiliesAnnually); // Families plan has no SecretsManager
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, 0, 0));
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenNoPaymentMethodAndNonFreePlan()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually, gatewayCustomerId: null);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, 5, 10));
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenNoSubscriptionAndNonFreePlan()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually, gatewaySubscriptionId: null);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, 5, 10));
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenOrganizationIsManagedByMSP()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));
        _providerRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider { Type = ProviderType.Msp });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, 5, 10));
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenAdditionalSmSeatsIsNegative()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: -1, additionalServiceAccounts: 0));
        Assert.Contains("subtract Secrets Manager seats", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenTotalSmSeatsIsZero()
    {
        // Enterprise plan has BaseSeats = 0; adding 0 extra means 0 total seats.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 0, additionalServiceAccounts: 0));
        Assert.Contains("do not have any Secrets Manager seats", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenPlanDoesNotAllowAdditionalServiceAccounts()
    {
        // Free plan has HasAdditionalServiceAccountOption = false.
        var organization = CreateOrganization(PlanType.Free, gatewayCustomerId: null, gatewaySubscriptionId: null, seats: 2);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.Free));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 0, additionalServiceAccounts: 1));
        Assert.Contains("does not allow additional Machine Accounts", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenSmSeatsExceedOrgSeats_NonTeamsStarterPlan()
    {
        // Enterprise: non-TeamsStarter plan — additionalSmSeats must not exceed org.Seats.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually, seats: 10);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 11, additionalServiceAccounts: 0));
        Assert.Contains("cannot have more Secrets Manager seats than Password Manager seats", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenSmSeatsExceedPasswordManagerBaseSeats_TeamsStarterPlan()
    {
        // TeamsStarter: additionalSmSeats must not exceed plan.PasswordManager.BaseSeats (= 10).
        var organization = CreateOrganization(PlanType.TeamsStarter);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.TeamsStarter));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 11, additionalServiceAccounts: 0));
        Assert.Contains("cannot have more Secrets Manager seats than Password Manager seats", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenAdditionalServiceAccountsIsNegative()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 5, additionalServiceAccounts: -1));
        Assert.Contains("subtract Machine Accounts", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenPlanDoesNotAllowAdditionalSeats()
    {
        // Free plan has HasAdditionalSeatsOption = false; requesting extra seats must throw.
        var organization = CreateOrganization(PlanType.Free, gatewayCustomerId: null, gatewaySubscriptionId: null, seats: 2);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.Free));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 1, additionalServiceAccounts: 0));
        Assert.Contains("does not allow additional users", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ThrowsBadRequest_WhenAdditionalSeatsExceedPlanMaximum()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = new PlanWithMaxAdditionalSeats(maxAdditionalSeats: 3);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 4, additionalServiceAccounts: 0));
        Assert.Contains("maximum of 3 additional users", exception.Message);
    }

    [Fact]
    public async Task RunAsync_DoesNotThrow_WhenPlanIsDisabled()
    {
        // A disabled plan must not block an existing subscriber from adding SM.
        // The old command delegated to ValidateSecretsManagerPlan which internally called
        // ValidatePlan — that method threw when plan.Disabled was true. The new command
        // intentionally omits the Disabled check.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var disabledPlan = new DisabledEnterprisePlan();
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(disabledPlan);
        SetupSubscriptionCommandSuccess();

        await _command.RunAsync(organization, 5, 10);
    }

    [Fact]
    public async Task RunAsync_CallsUpdateSubscriptionCommand_WithCorrectChangeSet_WhenNonFreePlan()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        SetupSubscriptionCommandSuccess();

        await _command.RunAsync(organization, additionalSmSeats: 5, additionalServiceAccounts: 10);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count == 2 &&
                cs.ChargeImmediately &&
                cs.Changes.Any(c => c.IsT0 && c.AsT0.PriceId == plan.SecretsManager.StripeSeatPlanId && c.AsT0.Quantity == 5) &&
                cs.Changes.Any(c => c.IsT0 && c.AsT0.PriceId == plan.SecretsManager.StripeServiceAccountPlanId && c.AsT0.Quantity == 10)));
    }

    [Fact]
    public async Task RunAsync_SkipsUpdateSubscriptionCommand_WhenFreePlan()
    {
        var organization = CreateOrganization(PlanType.Free, gatewayCustomerId: null, gatewaySubscriptionId: null, seats: 2);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.Free));

        await _command.RunAsync(organization, additionalSmSeats: 0, additionalServiceAccounts: 0);

        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
    }

    [Fact]
    public async Task RunAsync_SkipsUpdateSubscriptionCommand_WhenBothAdditionsAreZero_NonFreePlan()
    {
        // Uses a custom plan with SM BaseSeats > 0 so that 0 additional seats passes the
        // "you have no SM seats" validation and we reach the empty-change-set guard.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = new EnterprisePlanWithSmBaseSeats();
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        await _command.RunAsync(organization, additionalSmSeats: 0, additionalServiceAccounts: 0);

        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
    }

    [Fact]
    public async Task RunAsync_CallsUpdateSubscriptionCommand_WithOnlySeatChange_WhenServiceAccountsAreZero()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        SetupSubscriptionCommandSuccess();

        await _command.RunAsync(organization, additionalSmSeats: 5, additionalServiceAccounts: 0);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count == 1 &&
                cs.ChargeImmediately &&
                cs.Changes.Any(c => c.IsT0 && c.AsT0.PriceId == plan.SecretsManager.StripeSeatPlanId && c.AsT0.Quantity == 5)));
    }

    [Fact]
    public async Task RunAsync_CallsUpdateSubscriptionCommand_WithOnlyServiceAccountChange_WhenSeatsAreZero()
    {
        // Uses EnterprisePlanWithSmBaseSeats (BaseSeats = 5) so 0 additional seats passes
        // the "you have no SM seats" validation.
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = new EnterprisePlanWithSmBaseSeats();
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        SetupSubscriptionCommandSuccess();

        await _command.RunAsync(organization, additionalSmSeats: 0, additionalServiceAccounts: 10);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count == 1 &&
                cs.ChargeImmediately &&
                cs.Changes.Any(c => c.IsT0 && c.AsT0.PriceId == plan.SecretsManager.StripeServiceAccountPlanId && c.AsT0.Quantity == 10)));
    }

    [Fact]
    public async Task RunAsync_ThrowsBillingException_WhenUpdateSubscriptionCommandFails()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.EnterpriseAnnually));

        BillingCommandResult<Subscription> failure = new BadRequest("Stripe failure");
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(failure);

        await Assert.ThrowsAsync<BillingException>(() =>
            _command.RunAsync(organization, additionalSmSeats: 5, additionalServiceAccounts: 10));

        await _organizationService.DidNotReceiveWithAnyArgs().ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task RunAsync_UpdatesOrganizationAndCache_OnSuccess()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        SetupSubscriptionCommandSuccess();

        await _command.RunAsync(organization, additionalSmSeats: 5, additionalServiceAccounts: 10);

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
    /// An enterprise plan that caps additional SM seats at <paramref name="maxAdditionalSeats"/>,
    /// used to exercise the <see cref="AddSecretsManagerSubscriptionCommand"/> MaxAdditionalSeats check.
    /// </summary>
    private sealed record PlanWithMaxAdditionalSeats : StaticStorePlan
    {
        public PlanWithMaxAdditionalSeats(int maxAdditionalSeats)
        {
            Type = PlanType.EnterpriseAnnually;
            ProductTier = ProductTierType.Enterprise;
            Name = "Enterprise (Annually)";
            NameLocalizationKey = "planNameEnterprise";
            DescriptionLocalizationKey = "planDescEnterprise";
            PasswordManager = new PasswordManagerPlanFeatures
            {
                BaseSeats = 0,
                HasAdditionalSeatsOption = true,
                StripeSeatPlanId = "2023-enterprise-org-seat-annually"
            };
            SecretsManager = new SecretsManagerPlanFeatures
            {
                BaseSeats = 0,
                BaseServiceAccount = 50,
                HasAdditionalSeatsOption = true,
                HasAdditionalServiceAccountOption = true,
                MaxAdditionalSeats = maxAdditionalSeats,
                StripeSeatPlanId = "secrets-manager-enterprise-seat-annually",
                StripeServiceAccountPlanId = "secrets-manager-service-account-2024-annually"
            };
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
