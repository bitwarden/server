using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);
        var paymentIntentClientSecret = "CLIENT_SECRET";

        var organizationUser = new OrganizationUser
        {
            Id = organizationId,
            AccessSecretsManager = false,
            Type = OrganizationUserType.Owner
        };

        var organizationUsers = new List<OrganizationUser> { organizationUser };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationId)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(organization.Id,
            OrganizationUserType.Owner).Returns(organizationUsers);

        var result = await sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts);

        await sutProvider.GetDependency<IPaymentService>().Received()
            .AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceManyAsync(organizationUsers);

        Assert.NotNull(result);
        Assert.IsType<Organization>(result);
        Assert.Equal(additionalServiceAccounts, organization.SmServiceAccounts);
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
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    public async void ValidateSecretsManagerPlan_ThrowsException_WhenInvalidPlanSelected(PlanType planType, SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.Plans.FirstOrDefault(x => x.Type == planType);
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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);
        var additionalSeats = 1;
        var additionalServiceAccounts = 0;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Invalid Secrets Manager plan selected.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenNoSecretsManagerSeats(PlanType planType, SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
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
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);
        var additionalSeats = 0;
        var additionalServiceAccounts = 5;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You do not have any Secrets Manager seats!", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenAddingSeatWithNoAdditionalSeat(PlanType planType, SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = planType,
            SmSeats = 5,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);
        var additionalSeats = -5;
        var additionalServiceAccounts = 5;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You do not have any Secrets Manager seats!", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingSeats(PlanType planType, SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = planType,
            SmSeats = 5,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);
        var additionalSeats = -1;
        var additionalServiceAccounts = 5;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You can't subtract Secrets Manager seats!", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalServiceAccounts(SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == PlanType.Free);
        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = PlanType.Free,
            SmSeats = 5,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);
        var additionalSeats = 2;
        var additionalServiceAccounts = 5;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Plan does not allow additional Service Accounts.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenMoreSeatsThanPasswordManagerSeats(PlanType planType, SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == PlanType.Free);
        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = planType,
            SmSeats = 5,
            Seats = 5,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You cannot have more Secrets Manager seats than Password Manager seats.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingServiceAccounts(
        PlanType planType,
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == PlanType.Free);
        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = planType,
            SmSeats = 5,
            Seats = 5,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        var additionalSeats = 5;
        var additionalServiceAccounts = -5;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You can't subtract Service Accounts!", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalUsers(
        PlanType planType,
        SutProvider<AddSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == PlanType.Free);
        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "123",
            PlanType = planType,
            SmSeats = 5,
            Seats = 5,
            SmServiceAccounts = 0,
            UseSecretsManager = false,
            GatewaySubscriptionId = "1"
        };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        var additionalSeats = 2;
        var additionalServiceAccounts = 0;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organization, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Plan does not allow additional users.", exception.Message);
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
