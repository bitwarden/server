﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
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
    private static TheoryData<Plan> ToPlanTheory(List<PlanType> types)
    {
        var theoryData = new TheoryData<Plan>();
        var plans = types.Select(StaticStore.GetPlan).ToArray();
        theoryData.AddRange(plans);
        return theoryData;
    }

    public static TheoryData<Plan> AllTeamsAndEnterprise
        => ToPlanTheory([
            PlanType.EnterpriseAnnually2019,
            PlanType.EnterpriseAnnually2020,
            PlanType.EnterpriseAnnually,
            PlanType.EnterpriseMonthly2019,
            PlanType.EnterpriseMonthly2020,
            PlanType.EnterpriseMonthly,
            PlanType.TeamsMonthly2019,
            PlanType.TeamsMonthly2020,
            PlanType.TeamsMonthly,
            PlanType.TeamsAnnually2019,
            PlanType.TeamsAnnually2020,
            PlanType.TeamsAnnually,
            PlanType.TeamsStarter
        ]);

    public static TheoryData<Plan> CurrentTeamsAndEnterprise
        => ToPlanTheory([
            PlanType.EnterpriseAnnually,
            PlanType.EnterpriseMonthly,
            PlanType.TeamsMonthly,
            PlanType.TeamsAnnually,
            PlanType.TeamsStarter
        ]);

    [Theory]
    [BitMemberAutoData(nameof(AllTeamsAndEnterprise))]
    public async Task UpdateSubscriptionAsync_UpdateEverything_ValidInput_Passes(
        Plan plan,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = plan.Type;
        organization.Seats = 400;
        organization.SmSeats = 10;
        organization.MaxAutoscaleSmSeats = 20;
        organization.SmServiceAccounts = 200;
        organization.MaxAutoscaleSmServiceAccounts = 350;

        var updateSmSeats = 15;
        var updateSmServiceAccounts = 300;
        var updateMaxAutoscaleSmSeats = 16;
        var updateMaxAutoscaleSmServiceAccounts = 301;

        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmSeats = updateSmSeats,
            SmServiceAccounts = updateSmServiceAccounts,
            MaxAutoscaleSmSeats = updateMaxAutoscaleSmSeats,
            MaxAutoscaleSmServiceAccounts = updateMaxAutoscaleSmServiceAccounts
        };

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSmSeatsAsync(organization, plan, update.SmSeatsExcludingBase);
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
    [BitMemberAutoData(nameof(CurrentTeamsAndEnterprise))]
    public async Task UpdateSubscriptionAsync_ValidInput_WithNullMaxAutoscale_Passes(
        Plan plan,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = plan.Type;
        organization.Seats = 20;

        const int updateSmSeats = 15;
        const int updateSmServiceAccounts = 450;

        // Ensure that SmSeats is different from the original organization.SmSeats
        organization.SmSeats = updateSmSeats + 5;

        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmSeats = updateSmSeats,
            MaxAutoscaleSmSeats = null,
            SmServiceAccounts = updateSmServiceAccounts,
            MaxAutoscaleSmServiceAccounts = null
        };

        await sutProvider.Sut.UpdateSubscriptionAsync(update);

        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSmSeatsAsync(organization, plan, update.SmSeatsExcludingBase);
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, autoscaling).AdjustSeats(2);

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
        var plan = StaticStore.GetPlan(organization.PlanType);

        organization.UseSecretsManager = false;
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
             () => sutProvider.Sut.UpdateSubscriptionAsync(update));

        Assert.Contains("Organization has no access to Secrets Manager.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(AllTeamsAndEnterprise))]
    public async Task UpdateSubscriptionAsync_PaidPlan_NullGatewayCustomerId_ThrowsException(
        Plan plan,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = plan.Type;
        organization.GatewayCustomerId = null;

        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("No payment method found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(AllTeamsAndEnterprise))]
    public async Task UpdateSubscriptionAsync_PaidPlan_NullGatewaySubscriptionId_ThrowsException(
        Plan plan,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = plan.Type;
        organization.GatewaySubscriptionId = null;
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("No subscription found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(AllTeamsAndEnterprise))]
    public async Task AdjustServiceAccountsAsync_WithEnterpriseOrTeamsPlans_Success(
        Plan plan,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationSeats = plan.SecretsManager.BaseSeats + 10;
        var organizationMaxAutoscaleSeats = 20;
        var organizationServiceAccounts = plan.SecretsManager.BaseServiceAccount + 10;
        var organizationMaxAutoscaleServiceAccounts = 300;

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = plan.Type,
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
        var expectedSmServiceAccountsExcludingBase = expectedSmServiceAccounts - plan.SecretsManager.BaseServiceAccount;

        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustServiceAccounts(10);

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
        const int seatCount = 10;

        // Make sure Password Manager seats is greater or equal to Secrets Manager seats
        organization.Seats = seatCount;
        var plan = StaticStore.GetPlan(organization.PlanType);

        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmSeats = seatCount,
            MaxAutoscaleSmSeats = seatCount
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));

        Assert.Contains("Organization has no Secrets Manager seat limit, no need to adjust seats", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_SmSeatAutoscaling_Subtracting_ThrowsBadRequestException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, true).AdjustSeats(-2);

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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustSeats(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("You have reached the maximum number of Secrets Manager seats (2) for this plan",
            exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SmSeatAutoscaling_MaxLimitReached_ThrowsBadRequestException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.SmSeats = 9;
        organization.MaxAutoscaleSmSeats = 10;

        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, true).AdjustSeats(2);

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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmSeats = organization.SmSeats + 10,
            MaxAutoscaleSmSeats = organization.SmSeats + 5
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
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
        organization.SmSeats = 8;
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmSeats = 7,
        };

        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id).Returns(8);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("8 users are currently occupying Secrets Manager seats. You cannot decrease your subscription below your current occupied seat count", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_UpdateServiceAccountsToAutoscaleLimit_EmailsOwners(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustServiceAccounts(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Organization has no machine accounts limit, no need to adjust machine accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ServiceAccountAutoscaling_Subtracting_ThrowsBadRequestException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, true).AdjustServiceAccounts(-2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot use autoscaling to subtract machine accounts.", exception.Message);
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false).AdjustServiceAccounts(1);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("You have reached the maximum number of machine accounts (3) for this plan",
            exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task ServiceAccountAutoscaling_MaxLimitReached_ThrowsBadRequestException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.SmServiceAccounts = 9;
        organization.MaxAutoscaleSmServiceAccounts = 10;

        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, true).AdjustServiceAccounts(2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Secrets Manager machine account limit has been reached.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ServiceAccountsGreaterThanMaxAutoscaleSeats_ThrowsException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        const int smServiceAccount = 15;
        const int maxAutoscaleSmServiceAccounts = 10;

        organization.SmServiceAccounts = smServiceAccount - 5;
        organization.MaxAutoscaleSmServiceAccounts = 2 * smServiceAccount;

        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmServiceAccounts = smServiceAccount,
            MaxAutoscaleSmServiceAccounts = maxAutoscaleSmServiceAccounts
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot set max machine accounts autoscaling below machine account amount", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ServiceAccountsLessThanPlanMinimum_ThrowsException(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        const int newSmServiceAccounts = 49;

        organization.SmServiceAccounts = newSmServiceAccounts - 10;

        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmServiceAccounts = newSmServiceAccounts,
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Plan has a minimum of 50 machine accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(AllTeamsAndEnterprise))]
    public async Task UpdateSmServiceAccounts_WhenCurrentServiceAccountsIsGreaterThanNew_ThrowsBadRequestException(
        Plan plan,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var currentServiceAccounts = 301;
        organization.PlanType = plan.Type;
        organization.SmServiceAccounts = currentServiceAccounts;
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false) { SmServiceAccounts = 201 };

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your organization currently has 301 machine accounts. You cannot decrease your subscription below your current machine account usage", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowSeatCount(
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        const int smSeats = 10;
        const int maxAutoscaleSmSeats = 5;

        organization.SmSeats = smSeats - 1;
        organization.MaxAutoscaleSmSeats = smSeats * 2;

        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmSeats = smSeats,
            MaxAutoscaleSmSeats = maxAutoscaleSmSeats
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
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
        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false)
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

        var plan = StaticStore.GetPlan(organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate(organization, plan, false) { MaxAutoscaleSmServiceAccounts = 3 };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Your plan does not allow machine accounts autoscaling.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustSmSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustServiceAccountsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        // TODO: call ReferenceEventService - see AC-1481
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationMaxSeatLimitReachedEmailAsync(Arg.Any<Organization>(), Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IApplicationCacheService>().DidNotReceiveWithAnyArgs().UpsertOrganizationAbilityAsync(default);
    }

    private void AssertUpdatedOrganization(Func<Organization> organizationMatcher, SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organizationMatcher());
        sutProvider.GetDependency<IApplicationCacheService>().Received(1).UpsertOrganizationAbilityAsync(organizationMatcher());
    }
}
