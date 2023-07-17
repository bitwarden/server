using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Repositories;
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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationService>().ValidateSecretsManagerPlan(plan, signup);

        var result = await sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts);

        await sutProvider.GetDependency<IPaymentService>().Received()
            .AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(organization);
        sutProvider.GetDependency<IOrganizationService>().Received(1).ValidateSecretsManagerPlan(plan, signup);

        Assert.NotNull(result);
        Assert.IsType<Organization>(result);
        Assert.True(organization.UseSecretsManager);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsNotFoundException_WhenOrganizationIsNull(SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns((Organization)null);

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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

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

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceive().ReplaceManyAsync(Arg.Any<ICollection<OrganizationUser>>());
    }
}
