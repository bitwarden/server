using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
public class SubscribeOrganizationSmCommandTests
{
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task SignUpAsync_ReturnsSuccessAndClientSecret_WhenOrganizationAndPlanExist(PlanType planType,
        SutProvider<SubscribeOrganizationSmCommand> sutProvider)
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
            UseSecretsManager = false
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

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_ThrowsNotFoundException_WhenOrganizationIsNull(SutProvider<SubscribeOrganizationSmCommand> sutProvider)
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
    public async Task SignUpAsync_ThrowsGatewayException_WhenGatewayCustomerIdIsNullOrWhitespace(SutProvider<SubscribeOrganizationSmCommand> sutProvider)
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
}
