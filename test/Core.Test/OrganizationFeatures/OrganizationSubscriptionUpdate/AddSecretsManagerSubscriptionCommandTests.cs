using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
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
    [BitAutoData(PlanType.TeamsAnnually2019)]
    [BitAutoData(PlanType.TeamsAnnually2020)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsMonthly2020)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsStarter)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2020)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2020)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task SignUpAsync_ReturnsSuccessAndClientSecret_WhenOrganizationAndPlanExist(PlanType planType,
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        int additionalServiceAccounts,
        int additionalSmSeats,
        Organization organization,
        bool useSecretsManager)
    {
        organization.PlanType = planType;

        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);

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
            c.SmSeats == plan.SecretsManager.BaseSeats + additionalSmSeats &&
            c.SmServiceAccounts == plan.SecretsManager.BaseServiceAccount + additionalServiceAccounts &&
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

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsException_WhenOrganizationAlreadyHasSecretsManager(
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        Organization organization)
    {
        organization.UseSecretsManager = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(organization, 10, 10));

        Assert.Contains("Organization already uses Secrets Manager", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsException_WhenOrganizationIsManagedByMSP(
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider,
        Organization organization,
        Provider provider)
    {
        organization.UseSecretsManager = false;
        provider.Type = ProviderType.Msp;
        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id).Returns(provider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(organization, 10, 10));

        Assert.Contains("Organizations with a Managed Service Provider do not support Secrets Manager.", exception.Message);
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
