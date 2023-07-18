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
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = planType,
            SmSeats = 0,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = additionalSeats,
            AdditionalServiceAccounts = additionalServiceAccounts,
            AdditionalSeats = organization.Seats.GetValueOrDefault(),
        };

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        var result = await sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts);

        await sutProvider.GetDependency<IPaymentService>().Received()
            .AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(organization);
        sutProvider.GetDependency<IOrganizationService>().Received(1)
            .ValidateSecretsManagerPlan(plan, Arg.Is<OrganizationUpgrade>(c=>
                c.UseSecretsManager == signup.UseSecretsManager &&
                c.AdditionalSmSeats == signup.AdditionalSmSeats &&
                c.AdditionalServiceAccounts == signup.AdditionalServiceAccounts &&
                c.AdditionalSeats == organization.Seats.GetValueOrDefault()));

        Assert.NotNull(result);
        Assert.IsType<Organization>(result);
        Assert.True(organization.UseSecretsManager);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsNotFoundException_WhenOrganizationIsNull(SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.SignUpAsync(null, additionalSeats, additionalServiceAccounts));
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsGatewayException_WhenGatewayCustomerIdIsNullOrWhitespace(SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;
        var organization = new Organization { Id = organizationId };

        var exception = await Assert.ThrowsAsync<GatewayException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Not a gateway customer.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsGatewayException_WhenGatewaySubscriptionIdIsNullOrWhitespace(SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;
        var organization = new Organization { Id = organizationId, GatewayCustomerId = "1" };
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
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
