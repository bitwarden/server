using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Commands;

public class UpgradeOrganizationPlanVNextCommandTests
{
    private readonly IOrganizationBillingService _organizationBillingService = Substitute.For<IOrganizationBillingService>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand = Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly UpgradeOrganizationPlanVNextCommand _command;

    public UpgradeOrganizationPlanVNextCommandTests()
    {
        _command = new UpgradeOrganizationPlanVNextCommand(
            Substitute.For<ILogger<UpgradeOrganizationPlanVNextCommand>>(),
            _organizationBillingService,
            _organizationService,
            _pricingClient,
            _updateOrganizationSubscriptionCommand);
    }

    [Fact]
    public async Task Run_SamePlan_ReturnsBadRequest()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT1);
        Assert.Equal("Your organization is already on this plan.", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_Downgrade_ReturnsBadRequest()
    {
        var organization = CreateOrganization(PlanType.EnterpriseAnnually);
        var currentPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT1);
        Assert.Equal("You can't downgrade your organization's plan.", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_NoGatewayCustomerId_ReturnsConflict()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually, gatewayCustomerId: null);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT2);
    }

    [Fact]
    public async Task Run_UpgradeFromFree_FinalizesAndUpdatesOrganization()
    {
        var organization = CreateOrganization(
            PlanType.Free,
            gatewaySubscriptionId: null,
            seats: 2);
        var currentPlan = MockPlans.Get(PlanType.Free);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _organizationBillingService.Received(1).Finalize(Arg.Any<OrganizationSale>());
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization, null);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.Type, organization.PlanType);
        Assert.Equal(targetPlan.PasswordManager.BaseStorageGb, organization.MaxStorageGb);
        Assert.Null(organization.SmServiceAccounts);
    }

    [Fact]
    public async Task Run_UpgradeFromFree_WithKeys_BackfillsKeys()
    {
        var organization = CreateOrganization(
            PlanType.Free,
            gatewaySubscriptionId: null,
            seats: 2);
        var currentPlan = MockPlans.Get(PlanType.Free);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var keys = new PublicKeyEncryptionKeyPairData("wrappedPrivateKey", "publicKey");

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        var result = await _command.Run(organization, targetPlan, keys);

        Assert.True(result.IsT0);
        Assert.Equal("publicKey", organization.PublicKey);
        Assert.Equal("wrappedPrivateKey", organization.PrivateKey);
    }

    [Fact]
    public async Task Run_UpgradeFromFree_SetsSecretsManagerOnSale()
    {
        var organization = CreateOrganization(
            PlanType.Free,
            gatewaySubscriptionId: null,
            seats: 2,
            useSecretsManager: true,
            smSeats: 2);
        var currentPlan = MockPlans.Get(PlanType.Free);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _organizationBillingService.Received(1).Finalize(
            Arg.Is<OrganizationSale>(s =>
                s.SubscriptionSetup.SecretsManagerOptions != null));
    }

    [Fact]
    public async Task Run_UpgradeFromFree_WithSecretsManager_SetsSmServiceAccountsToNewPlanBase()
    {
        var freePlan = MockPlans.Get(PlanType.Free);
        var organization = CreateOrganization(
            PlanType.Free,
            gatewaySubscriptionId: null,
            seats: 2,
            useSecretsManager: true,
            smSeats: 2,
            smServiceAccounts: freePlan.SecretsManager.BaseServiceAccount);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(freePlan);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        Assert.Equal(targetPlan.SecretsManager.BaseServiceAccount, organization.SmServiceAccounts);
    }

    [Fact]
    public async Task Run_PaidUpgrade_ChangesPasswordManagerPrice()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count >= 1 &&
                cs.ChargeImmediately));
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization, null);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.Type, organization.PlanType);
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithExtraStorage_ChangesStoragePrice()
    {
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var organization = CreateOrganization(
            PlanType.TeamsAnnually,
            maxStorageGb: (short)(currentPlan.PasswordManager.BaseStorageGb + 1));
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count == 2 &&
                cs.ChargeImmediately));
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithSecretsManager_ChangesSmSeatPrice()
    {
        var organization = CreateOrganization(
            PlanType.TeamsAnnually,
            useSecretsManager: true,
            smSeats: 5);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count >= 2 &&
                cs.ChargeImmediately));
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithSmServiceAccountsAboveBase_ChangesServiceAccountPrice()
    {
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var organization = CreateOrganization(
            PlanType.TeamsAnnually,
            useSecretsManager: true,
            smSeats: 5,
            smServiceAccounts: currentPlan.SecretsManager.BaseServiceAccount + 10);

        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        // PM seat + SM seat + SM service account = 3 price changes
        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Count == 3 &&
                cs.ChargeImmediately));
    }

    [Fact]
    public async Task Run_PaidUpgrade_UpdatesAllOrganizationPlanProperties()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        await _command.Run(organization, targetPlan, null);

        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.Type, organization.PlanType);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasKeyConnector, organization.UseKeyConnector);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.AutomaticUserConfirmation, organization.UseAutomaticUserConfirmation);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithKeys_BackfillsKeys()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        var keys = new PublicKeyEncryptionKeyPairData("wrappedPrivateKey", "publicKey");

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        await _command.Run(organization, targetPlan, keys);

        Assert.Equal("publicKey", organization.PublicKey);
        Assert.Equal("wrappedPrivateKey", organization.PrivateKey);
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithoutKeys_DoesNotOverwriteKeys()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        organization.PublicKey = "existingPublic";
        organization.PrivateKey = "existingPrivate";
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        await _command.Run(organization, targetPlan, null);

        Assert.Equal("existingPublic", organization.PublicKey);
        Assert.Equal("existingPrivate", organization.PrivateKey);
    }

    [Fact]
    public async Task Run_PaidUpgrade_CommandFailure_PropagatesResult()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        BillingCommandResult<Subscription> failureResult = new BadRequest("Stripe error");
        _updateOrganizationSubscriptionCommand
            .Run(organization, Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(failureResult);

        var result = await _command.Run(organization, targetPlan, null);

        // Result is mapped through — BadRequest becomes T1
        Assert.True(result.IsT1);
        await _organizationService.DidNotReceive().ReplaceAndUpdateCacheAsync(Arg.Any<Organization>(), Arg.Any<EventType?>());
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
        int? seats = 10,
        short? maxStorageGb = null,
        bool useSecretsManager = false,
        int? smSeats = null,
        int? smServiceAccounts = null) => new()
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            Plan = MockPlans.Get(planType).Name,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId,
            Seats = seats,
            MaxStorageGb = maxStorageGb,
            UseSecretsManager = useSecretsManager,
            SmSeats = smSeats,
            SmServiceAccounts = smServiceAccounts
        };
}
