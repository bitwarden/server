using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Stripe.TestHelpers;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Organizations.Commands;

public class UpgradeOrganizationPlanVNextCommandTests
{
    private readonly IOrganizationBillingService _organizationBillingService = Substitute.For<IOrganizationBillingService>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand = Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private static readonly DateTime _frozen = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly UpgradeOrganizationPlanVNextCommand _command;

    public UpgradeOrganizationPlanVNextCommandTests()
    {
        _command = new UpgradeOrganizationPlanVNextCommand(
            Substitute.For<ILogger<UpgradeOrganizationPlanVNextCommand>>(),
            _organizationBillingService,
            _organizationService,
            _pricingClient,
            _priceIncreaseScheduler,
            _stripeAdapter,
            _updateOrganizationSubscriptionCommand,
            new FakeTimeProvider());
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
        Assert.False(targetPlan.HasRiskInsights);
        Assert.Equal(targetPlan.HasRiskInsights, organization.UseRiskInsights);
        Assert.False(organization.UseRiskInsights);
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

    // Regression (PM-40866): a Teams 2019 org at/under its 5 included seats has only the base bundle
    // line, so the base line is repointed to the Enterprise seat price at the org's seat count with no
    // overage removal
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_Teams2019ToEnterprise_AtBaseSeats_ChangesBasePriceWithoutRemovingOverage(bool isAnnual)
    {
        var currentPlan = MockPlans.Get(isAnnual ? PlanType.TeamsAnnually2019 : PlanType.TeamsMonthly2019);
        var targetPlan = MockPlans.Get(isAnnual ? PlanType.EnterpriseAnnually : PlanType.EnterpriseMonthly);
        var organization = CreateOrganization(
            isAnnual ? PlanType.TeamsAnnually2019 : PlanType.TeamsMonthly2019, seats: 5);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                cs.Changes.Any(c => c.IsT1
                    && c.AsT1.CurrentPriceId == currentPlan.PasswordManager.StripePlanId
                    && c.AsT1.UpdatedPriceId == targetPlan.PasswordManager.StripeSeatPlanId
                    && c.AsT1.Quantity == organization.Seats)
                && cs.Changes.All(c => !c.IsT2)));
    }

    // Regression (PM-40866): a Teams 2019 org above its 5 included seats carries a per-seat overage line
    // on top of the base bundle. The upgrade must collapse both onto one Enterprise seat line - repoint
    // the base line to the Enterprise seat price at the org's seat count, AND remove the overage line so
    // it doesn't linger next to the new line and double-bill.
    [Fact]
    public async Task Run_Teams2019ToEnterprise_AboveBaseSeats_RemovesOverage()
    {
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually2019);           // base + overage, BaseSeats = 5
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        var organization = CreateOrganization(PlanType.TeamsAnnually2019, seats: 10);   // 10 > 5 => has an overage line
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        // Capture the change set handed to the update command so we can assert on its individual changes.
        OrganizationSubscriptionChangeSet changeSet = null;
        BillingCommandResult<Subscription> success = new Subscription();
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<Organization>(), Arg.Do<OrganizationSubscriptionChangeSet>(cs => changeSet = cs))
            .Returns(success);

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        Assert.NotNull(changeSet);

        // ChangeItemPrice (the T1 case of the OrganizationSubscriptionChange union): the base bundle line
        // is repointed to the Enterprise seat price, quantity set to the org's total seats.
        var priceChange = Assert.Single(changeSet.Changes.Where(c => c.IsT1)).AsT1;
        Assert.Equal(currentPlan.PasswordManager.StripePlanId, priceChange.CurrentPriceId);   // from: base bundle
        Assert.Equal(targetPlan.PasswordManager.StripeSeatPlanId, priceChange.UpdatedPriceId); // to: Enterprise seat
        Assert.Equal(organization.Seats, priceChange.Quantity);

        // RemoveItem (the T2 case): the current plan's per-seat overage line is dropped, so it collapses
        // into the single Enterprise seat line instead of remaining as a separate charge.
        var removal = Assert.Single(changeSet.Changes.Where(c => c.IsT2)).AsT2;
        Assert.Equal(currentPlan.PasswordManager.StripeSeatPlanId, removal.PriceId);
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
        Assert.Equal(targetPlan.HasRiskInsights, organization.UseRiskInsights);
        Assert.True(targetPlan.HasRiskInsights);
        Assert.True(organization.UseRiskInsights);
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

    [Fact]
    public async Task Run_PaidUpgrade_TrackAOrg_ReleasesScheduleWithOrganizationId()
    {
        // Track A: a 2020-era source plan upgrading to a current plan. Tiers ascend
        // (Teams sort order 3 -> Enterprise sort order 4) so the upgrade is accepted.
        var organization = CreateOrganization(PlanType.TeamsAnnually2020);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually2020);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).Release(
            organization.GatewayCustomerId!,
            organization.GatewaySubscriptionId!,
            organization.Id);
    }

    // PM-37510 (T11): a voluntary paid upgrade forfeits SM service-account grace by clearing the
    // metadata key (empty string removes it in Stripe). Subsequent billing math then reads grace 0.
    [Fact]
    public async Task Run_PaidUpgrade_ForfeitsGraceMetadata()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually2020);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually2020);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            organization.GatewaySubscriptionId!,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == string.Empty));
    }

    [Fact]
    public async Task Run_PaidUpgrade_CommandFailure_DoesNotForfeitGraceMetadata()
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

        Assert.True(result.IsT1);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Run_PaidUpgrade_TwentyTwentyToTwentyTwenty_ReleasesScheduleWithOrganizationId()
    {
        // 2020-era source upgrading to a 2020-era target across ascending tiers
        // (Teams 2020 sort order 3 -> Enterprise 2020 sort order 4).
        var organization = CreateOrganization(PlanType.TeamsAnnually2020);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually2020);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually2020);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).Release(
            organization.GatewayCustomerId!,
            organization.GatewaySubscriptionId!,
            organization.Id);
    }

    [Fact]
    public async Task Run_UpgradeFromFree_DoesNotReleaseSchedule()
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
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Run_PaidUpgrade_NonTrackAOrg_StillReleasesScheduleWithOrganizationId()
    {
        // Non-Track-A: a current (non-2020) source plan still releases on upgrade.
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        SetupSubscriptionCommandSuccess();

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).Release(
            organization.GatewayCustomerId!,
            organization.GatewaySubscriptionId!,
            organization.Id);
    }

    [Fact]
    public async Task Run_PaidUpgrade_ReleaseThrows_AbortsUpgrade()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        var currentPlan = MockPlans.Get(PlanType.TeamsAnnually);
        var targetPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);

        _priceIncreaseScheduler
            .Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>())
            .ThrowsAsync(new InvalidOperationException("release failed"));

        var result = await _command.Run(organization, targetPlan, null);

        // HandleAsync converts the thrown exception into an Unhandled error result.
        Assert.True(result.IsT3);
        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>(), Arg.Any<EventType?>());
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithProrationDateBeforeCurrentPeriod_ThrowsBeforeAnyWrite()
    {
        var (organization, targetPlan) = SetupPaidUpgrade(_frozen.AddMinutes(-5));

        await Assert.ThrowsAsync<InvoicePreviewExpiredException>(() =>
            _command.Run(organization, targetPlan, null, _frozen.AddMinutes(-10)));

        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithValidProrationDate_PassesItToSubscriptionUpdate()
    {
        var (organization, targetPlan) = SetupPaidUpgrade();
        var prorationDate = _frozen.AddMinutes(-10);

        var result = await _command.Run(organization, targetPlan, null, prorationDate);

        Assert.True(result.IsT0);
        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Any<OrganizationSubscriptionChangeSet>(),
            null,
            prorationDate);
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithUnspecifiedKindProrationDate_PassesItToSubscriptionUpdateAsUtc()
    {
        var (organization, targetPlan) = SetupPaidUpgrade();
        var unspecified = DateTime.SpecifyKind(_frozen.AddMinutes(-10), DateTimeKind.Unspecified);

        var result = await _command.Run(organization, targetPlan, null, unspecified);

        Assert.True(result.IsT0);
        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Any<OrganizationSubscriptionChangeSet>(),
            null,
            Arg.Is<DateTime?>(d => d.HasValue && d.Value.Kind == DateTimeKind.Utc && d.Value.Ticks == unspecified.Ticks));
    }

    [Theory]
    [InlineData(-31 * 60)]
    [InlineData(1)]
    public async Task Run_PaidUpgrade_WithInvalidProrationDate_ThrowsBeforeAnyWrite(int secondsFromFrozen)
    {
        var (organization, targetPlan) = SetupPaidUpgrade();

        await Assert.ThrowsAsync<InvoicePreviewExpiredException>(() =>
            _command.Run(organization, targetPlan, null, _frozen.AddSeconds(secondsFromFrozen)));

        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await _updateOrganizationSubscriptionCommand.DidNotReceiveWithAnyArgs()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>());
        await _organizationService.DidNotReceiveWithAnyArgs()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>(), Arg.Any<EventType?>());
    }

    [Fact]
    public async Task Run_PaidUpgrade_FetchesSubscriptionWithTestClockExpanded_WhenProrationDateGiven()
    {
        var (organization, targetPlan) = SetupPaidUpgrade();

        await _command.Run(organization, targetPlan, null, _frozen.AddMinutes(-10));

        await _stripeAdapter.Received(1).GetSubscriptionAsync(
            organization.GatewaySubscriptionId!,
            Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("test_clock")));
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithoutProrationDate_DoesNotFetchSubscriptionAndPassesNull()
    {
        var (organization, targetPlan) = SetupPaidUpgrade();

        var result = await _command.Run(organization, targetPlan, null);

        Assert.True(result.IsT0);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Any<OrganizationSubscriptionChangeSet>(),
            null,
            null);
    }

    [Fact]
    public async Task Run_PaidUpgrade_WithProrationDate_SubscriptionNotFound_ReturnsConflictBeforeAnyWrite()
    {
        var (organization, targetPlan) = SetupPaidUpgrade();
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId!, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var result = await _command.Run(organization, targetPlan, null, _frozen.AddMinutes(-10));

        Assert.True(result.IsT2);
        Assert.Equal("We couldn't find your subscription.", result.AsT2.Response);
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .Release(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Run_UpgradeFromFree_WithExpiredProrationDate_IgnoresIt()
    {
        var organization = CreateOrganization(PlanType.Free, gatewaySubscriptionId: null, seats: 2);
        var targetPlan = MockPlans.Get(PlanType.TeamsAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.Free));

        var result = await _command.Run(organization, targetPlan, null, DateTime.UtcNow.AddDays(-1));

        Assert.True(result.IsT0);
        await _organizationBillingService.Received(1).Finalize(Arg.Any<OrganizationSale>());
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    private (Organization Organization, Bit.Core.Models.StaticStore.Plan TargetPlan) SetupPaidUpgrade(DateTime? currentPeriodStart = null)
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(PlanType.TeamsAnnually));
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId!, Arg.Any<SubscriptionGetOptions>())
            .Returns(new Subscription
            {
                TestClock = new TestClock { FrozenTime = _frozen },
                Items = new StripeList<SubscriptionItem>
                {
                    Data = [new SubscriptionItem { CurrentPeriodStart = currentPeriodStart ?? _frozen.AddDays(-180) }]
                }
            });
        SetupSubscriptionCommandSuccess();
        return (organization, MockPlans.Get(PlanType.EnterpriseAnnually));
    }

    private void SetupSubscriptionCommandSuccess()
    {
        BillingCommandResult<Subscription> successResult = new Subscription();
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>(), Arg.Any<DateTime?>())
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
