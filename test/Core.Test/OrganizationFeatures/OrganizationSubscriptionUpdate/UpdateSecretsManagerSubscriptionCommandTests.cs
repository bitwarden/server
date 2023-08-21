using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
[SecretsManagerOrganizationCustomize]
public class UpdateSecretsManagerSubscriptionCommandTests
{
    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSubscriptionAsync_UpdateEverything_ValidInput_Passes(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.SmSeats = 10;
        organization.MaxAutoscaleSmSeats = 20;
        organization.SmServiceAccounts = 200;
        organization.MaxAutoscaleSmServiceAccounts = 350;

        var updateSmSeats = 15;
        var updateSmServiceAccounts = 300;
        var updateMaxAutoscaleSmSeats = 16;
        var updateMaxAutoscaleSmServiceAccounts = 301;

        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = updateSmSeats,
            SmServiceAccounts = updateSmServiceAccounts,
            MaxAutoscaleSmSeats = updateMaxAutoscaleSmSeats,
            MaxAutoscaleSmServiceAccounts = updateMaxAutoscaleSmServiceAccounts
        };

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSeatsAsync(organization, plan, update.SmSeatsExcludingBase);
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustServiceAccountsAsync(organization, plan, update.SmServiceAccountsExcludingBase);

        // TODO: call ReferenceEventService - see AC-1481

        AssertUpdatedOrganization(() => Arg.Is<Organization>(org =>
                org.Id == organization.Id &&
                org.SmSeats == updateSmSeats &&
                org.MaxAutoscaleSmSeats == updateMaxAutoscaleSmSeats &&
                org.SmServiceAccounts == updateSmServiceAccounts &&
                org.MaxAutoscaleSmServiceAccounts == updateMaxAutoscaleSmServiceAccounts),
                sutProvider);

        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendSecretsManagerMaxSeatLimitReachedEmailAsync(default, default, default);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSubscriptionAsync_ValidInput_WithNullMaxAutoscale_Passes(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;

        const int updateSmSeats = 15;
        const int updateSmServiceAccounts = 450;
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = updateSmSeats,
            MaxAutoscaleSmSeats = null,
            SmServiceAccounts = updateSmServiceAccounts,
            MaxAutoscaleSmServiceAccounts = null
        };

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSeatsAsync(organization, plan, update.SmSeatsExcludingBase);
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustServiceAccountsAsync(organization, plan, update.SmServiceAccountsExcludingBase);

        // TODO: call ReferenceEventService - see AC-1481

        AssertUpdatedOrganization(() => Arg.Is<Organization>(org =>
                org.Id == organization.Id &&
                org.SmSeats == updateSmSeats &&
                org.MaxAutoscaleSmSeats == null &&
                org.SmServiceAccounts == updateSmServiceAccounts &&
                org.MaxAutoscaleSmServiceAccounts == null),
            sutProvider);

        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendSecretsManagerMaxSeatLimitReachedEmailAsync(default, default, default);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(false, "Cannot update subscription on a self-hosted instance.")]
    [BitAutoData(true, "Cannot autoscale on a self-hosted instance.")]
    public async Task UpdatingSubscription_WhenSelfHosted_ThrowsBadRequestException(
        bool autoscaling,
        string expectedError,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UseSecretsManager = true;

        var update = new SecretsManagerSubscriptionUpdate(organization, autoscaling);
        update.AdjustSeats(2);

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains(expectedError, exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_NoSecretsManagerAccess_ThrowsException(
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider,
        Organization organization)
    {
        organization.UseSecretsManager = false;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
             () => sutProvider.Sut.UpdateSubscriptionAsync(update));

        Assert.Contains("Organization has no access to Secrets Manager.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSubscriptionAsync_PaidPlan_NullGatewayCustomerId_ThrowsException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.GatewayCustomerId = null;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("No payment method found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSubscriptionAsync_PaidPlan_NullGatewaySubscriptionId_ThrowsException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.GatewaySubscriptionId = null;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("No subscription found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task AdjustServiceAccountsAsync_WithEnterpriseOrTeamsPlans_Success(PlanType planType, Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == planType);

        var organizationSeats = plan.BaseSeats + 10;
        var organizationMaxAutoscaleSeats = 20;
        var organizationServiceAccounts = plan.BaseServiceAccount.GetValueOrDefault() + 10;
        var organizationMaxAutoscaleServiceAccounts = 300;

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
            UseSecretsManager = true,
            SmSeats = organizationSeats,
            MaxAutoscaleSmSeats = organizationMaxAutoscaleSeats,
            SmServiceAccounts = organizationServiceAccounts,
            MaxAutoscaleSmServiceAccounts = organizationMaxAutoscaleServiceAccounts
        };

        var smServiceAccountsAdjustment = 10;
        var expectedSmServiceAccounts = organizationServiceAccounts + smServiceAccountsAdjustment;
        var expectedSmServiceAccountsExcludingBase = expectedSmServiceAccounts - plan.BaseServiceAccount.GetValueOrDefault();

        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustServiceAccounts(10);

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustServiceAccountsAsync(
            Arg.Is<Organization>(o => o.Id == organizationId),
            plan,
            expectedSmServiceAccountsExcludingBase);
        // TODO: call ReferenceEventService - see AC-1481
        AssertUpdatedOrganization(() => Arg.Is<Organization>(o =>
                o.Id == organizationId
                && o.SmSeats == organizationSeats
                && o.MaxAutoscaleSmSeats == organizationMaxAutoscaleSeats
                && o.SmServiceAccounts == expectedSmServiceAccounts
                && o.MaxAutoscaleSmServiceAccounts == organizationMaxAutoscaleServiceAccounts), sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_UpdateSeatsToAutoscaleLimit_EmailsOwners(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.SmSeats = 9;
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = 10,
            MaxAutoscaleSmSeats = 10
        };

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        await sutProvider.GetDependency<IMailService>().Received(1).SendSecretsManagerMaxSeatLimitReachedEmailAsync(
            organization, organization.MaxAutoscaleSmSeats.Value, Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_OrgWithNullSmSeatOnSeatsAdjustment_ThrowsException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.SmSeats = null;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));

        Assert.Contains("Organization has no Secrets Manager seat limit, no need to adjust seats", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateSubscriptionAsync_SmSeatAutoscaling_Subtracting_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustSeats(-2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot use autoscaling to subtract seats.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateSubscriptionAsync_WithHasAdditionalSeatsOptionFalse_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("You have reached the maximum number of Secrets Manager seats (2) for this plan",
            exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SmSeatAutoscaling_MaxLimitReached_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;
        organization.SmSeats = 9;
        organization.MaxAutoscaleSmSeats = 10;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustSeats(2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Secrets Manager seat limit has been reached.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_SeatsAdjustmentGreaterThanMaxAutoscaleSeats_ThrowsException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = 15,
            MaxAutoscaleSmSeats = 10
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ThrowsBadRequestException_WhenSmSeatsLessThanOne(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = 0,
        };

        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id).Returns(8);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("You must have at least 1 Secrets Manager seat.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ThrowsBadRequestException_WhenOccupiedSeatsExceedNewSeatTotal(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = 7,
        };

        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id).Returns(8);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your organization currently has 8 Secrets Manager seats. Your plan only allows 7 Secrets Manager seats. Remove some Secrets Manager users", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_UpdateServiceAccountsToAutoscaleLimit_EmailsOwners(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.SmServiceAccounts = 250;
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmServiceAccounts = 300,
            MaxAutoscaleSmServiceAccounts = 300
        };

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        await sutProvider.GetDependency<IMailService>().Received(1).SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(
            organization, organization.MaxAutoscaleSmServiceAccounts.Value, Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountsAsync_ThrowsBadRequestException_WhenSmServiceAccountsIsNull(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.SmServiceAccounts = null;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustServiceAccounts(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Organization has no Service Accounts limit, no need to adjust Service Accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateSubscriptionAsync_ServiceAccountAutoscaling_Subtracting_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustServiceAccounts(-2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot use autoscaling to subtract service accounts.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateSubscriptionAsync_WithHasAdditionalServiceAccountOptionFalse_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        var update = new SecretsManagerSubscriptionUpdate(organization, false);
        update.AdjustServiceAccounts(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("You have reached the maximum number of service accounts (3) for this plan",
            exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task ServiceAccountAutoscaling_MaxLimitReached_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;
        organization.SmServiceAccounts = 9;
        organization.MaxAutoscaleSmServiceAccounts = 10;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustServiceAccounts(2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Secrets Manager service account limit has been reached.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ServiceAccountsGreaterThanMaxAutoscaleSeats_ThrowsException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmServiceAccounts = 15,
            MaxAutoscaleSmServiceAccounts = 10
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot set max service accounts autoscaling below service account amount", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ServiceAccountsLessThanPlanMinimum_ThrowsException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmServiceAccounts = 199,
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Plan has a minimum of 200 Service Accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSmServiceAccounts_WhenCurrentServiceAccountsIsGreaterThanNew_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var currentServiceAccounts = 301;
        organization.PlanType = planType;
        organization.SmServiceAccounts = currentServiceAccounts;
        var update = new SecretsManagerSubscriptionUpdate(organization, false) { SmServiceAccounts = 201 };

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your organization currently has 301 Service Accounts. Your plan only allows 201 Service Accounts. Remove some Service Accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowSeatCount(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            SmSeats = 10,
            MaxAutoscaleSmSeats = 5
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateMaxAutoscaleSmSeats_ThrowsBadRequestException_WhenExceedsPlanMaxUsers(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.SmSeats = 2;
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            MaxAutoscaleSmSeats = 3
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your plan has a Secrets Manager seat limit of 2, but you have specified a max autoscale count of 3.Reduce your max autoscale count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateMaxAutoscaleSmSeats_ThrowsBadRequestException_WhenPlanDoesNotAllowAutoscale(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.SmSeats = 2;
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            MaxAutoscaleSmSeats = 2
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your plan does not allow Secrets Manager seat autoscaling", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateMaxAutoscaleSmServiceAccounts_ThrowsBadRequestException_WhenPlanDoesNotAllowAutoscale(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.SmServiceAccounts = 3;

        var update = new SecretsManagerSubscriptionUpdate(organization, false) { MaxAutoscaleSmServiceAccounts = 3 };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your plan does not allow Service Accounts autoscaling.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustServiceAccountsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        // TODO: call ReferenceEventService - see AC-1481
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationMaxSeatLimitReachedEmailAsync(Arg.Any<Organization>(), Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());

        sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        sutProvider.GetDependency<IApplicationCacheService>().DidNotReceiveWithAnyArgs().UpsertOrganizationAbilityAsync(default);
    }

    private void AssertUpdatedOrganization(Func<Organization> organizationMatcher, SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organizationMatcher());
        sutProvider.GetDependency<IApplicationCacheService>().Received(1).UpsertOrganizationAbilityAsync(organizationMatcher());
    }
}
