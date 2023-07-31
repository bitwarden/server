using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;
[SutProviderCustomize]
public class AddSecretsManagerSubscriptionCommandTests
{
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task SignUpAsync_ReturnsSuccessAndClientSecret_WhenOrganizationAndPlanExist(PlanType planType,
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        int additionalServiceAccounts,
        int additionalSmSeats,
        Organization organization,
        bool useSecretsManager)
    {
        organization.PlanType = planType;

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        await sutProvider.Sut.SignUpAsync(organization, additionalSmSeats, additionalServiceAccounts);

        sutProvider.GetDependency<IOrganizationService>().Received(1)
            .ValidateSecretsManagerPlan(plan, Arg.Is<OrganizationUpgrade>(c =>
                c.UseSecretsManager == useSecretsManager &&
                c.AdditionalSmSeats == additionalSmSeats &&
                c.AdditionalServiceAccounts == additionalServiceAccounts &&
                c.AdditionalSeats == organization.Seats.GetValueOrDefault()));

        await sutProvider.GetDependency<IPaymentService>().Received()
            .AddSecretsManagerToSubscription(organization, plan, additionalSmSeats, additionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(Arg.Is<Organization>(c =>
            c.SmSeats == plan.BaseSeats + additionalSmSeats &&
            c.SmServiceAccounts == plan.BaseServiceAccount.GetValueOrDefault() + additionalServiceAccounts &&
            c.UseSecretsManager == true));
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsNotFoundException_WhenOrganizationIsNull(
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        int additionalServiceAccounts,
        int additionalSmSeats)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.SignUpAsync(null, additionalSmSeats, additionalServiceAccounts));
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsGatewayException_WhenGatewayCustomerIdIsNullOrWhitespace(
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        Organization organization,
        int additionalServiceAccounts,
        int additionalSmSeats)
    {
        organization.GatewayCustomerId = null;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SignUpAsync(organization, additionalSmSeats, additionalServiceAccounts));
        Assert.Contains("No payment method found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsGatewayException_WhenGatewaySubscriptionIdIsNullOrWhitespace(
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        Organization organization,
        int additionalServiceAccounts,
        int additionalSmSeats)
    {
        organization.GatewaySubscriptionId = null;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SignUpAsync(organization, additionalSmSeats, additionalServiceAccounts));
        Assert.Contains("No subscription found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AddSecretsManagerToSubscription(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>(), Arg.Any<int>());

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationService>().DidNotReceive().ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
    }
}
