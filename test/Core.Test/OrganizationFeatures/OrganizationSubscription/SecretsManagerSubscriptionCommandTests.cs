using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscription;
using Bit.Core.OrganizationFeatures.OrganizationSubscription.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscription;
[SutProviderCustomize]
public class SecretsManagerSubscriptionCommandTests
{
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task SignUpAsync_ReturnsSuccessAndClientSecret_WhenOrganizationAndPlanExist(PlanType planType,
        SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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

        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationId)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(organization.Id,
            OrganizationUserType.Owner).Returns(organizationUsers);

        var result = await sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts);

        await sutProvider.GetDependency<IPaymentService>().Received()
            .AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organization);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(organizationUser);

        Assert.NotNull(result);
        Assert.NotNull(result.Item1);
        Assert.NotNull(result.Item2);
        Assert.IsType<Tuple<Organization, OrganizationUser>>(result);
        Assert.Equal(additionalServiceAccounts, organization.SmServiceAccounts);
        Assert.True(organization.UseSecretsManager);
    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<SecretsManagerSubscriptionCommand> sutProvider, Organization organization, Plan plan,
        int additionalSeats, int additionalServiceAccounts, OrganizationUser organizationUser)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AddSecretsManagerToSubscription(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>(),Arg.Any<int>());
        // TODO: call ReferenceEventService - see AC-1481
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationMaxSeatLimitReachedEmailAsync(Arg.Any<Organization>(), Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());
        
        await sutProvider.GetDependency<IPaymentService>().Received()
            .AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organization);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(organizationUser);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsNotFoundException_WhenOrganizationIsNull(SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;

        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns((Organization)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsGatewayException_WhenGatewayCustomerIdIsNullOrWhitespace(SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;
        var organization = new Organization { Id = organizationId };

        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<GatewayException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Not a gateway customer.", exception.Message);
    }
    
    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    public async void ValidateSecretsManagerPlan_ThrowsException_WhenInvalidPlanSelected(PlanType planType, SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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

        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);
        var additionalSeats = 1;
        var additionalServiceAccounts = 0;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Invalid Secrets Manager plan selected.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenNoSecretsManagerSeats(PlanType planType, SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);
        var additionalSeats = 0;
        var additionalServiceAccounts = 5;
    
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You do not have any Secrets Manager seats!", exception.Message);
    }
    
    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenAddingSeatWithNoAdditionalSeat(PlanType planType, SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);
        var additionalSeats = -5;
        var additionalServiceAccounts = 5;
    
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You do not have any Secrets Manager seats!", exception.Message);
    }
    
    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingSeats(PlanType planType, SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);
        var additionalSeats = -1;
        var additionalServiceAccounts = 5;
    
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You can't subtract Secrets Manager seats!", exception.Message);
    }
    
    [Theory]
    [BitAutoData]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalServiceAccounts(SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId)
            .Returns(organization);
        var additionalSeats = 2;
        var additionalServiceAccounts = 5;
    
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Plan does not allow additional Service Accounts.", exception.Message);
    }
    
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenMoreSeatsThanPasswordManagerSeats(PlanType planType, SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId).Returns(organization);
        var additionalSeats = 10;
        var additionalServiceAccounts = 5;
    
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You cannot have more Secrets Manager seats than Password Manager seats.", exception.Message);
    }
    
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingServiceAccounts(PlanType planType, SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId).Returns(organization);
        var additionalSeats = 5;
        var additionalServiceAccounts = -5;
    
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("You can't subtract Service Accounts!", exception.Message);
    }
    
    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalUsers(
        PlanType planType,
        SutProvider<SecretsManagerSubscriptionCommand> sutProvider)
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
        sutProvider.GetDependency<IGetOrganizationQuery>().GetOrgById(organizationId).Returns(organization);
        var additionalSeats = 2;
        var additionalServiceAccounts = 0;
        
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(organizationId, additionalSeats, additionalServiceAccounts));
        Assert.Contains("Plan does not allow additional users.", exception.Message);
    }
}
