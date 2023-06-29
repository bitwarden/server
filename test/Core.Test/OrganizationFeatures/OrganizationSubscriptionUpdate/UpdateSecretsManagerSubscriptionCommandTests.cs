using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class UpdateSecretsManagerSubscriptionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NoOrganization_Throws(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = null,
            SeatAdjustment = 0
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_SeatsAdustmentGreaterThanMaxAutoscaleSeats_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 10,
            SeatAdjustment = 15
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ServiceAccountsGreaterThanMaxAutoscaleSeats_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 10,
            ServiceAccountsAdjustment = 11
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Cannot set max service account autoscaling below service account count.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NullGatewayCustomerId_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("No payment method found.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NullGatewaySubscriptionId_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1"
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("No subscription found.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_OrgWithNullSmSeatOnSeatsAdjustment_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = null,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1"
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Organization has no Secrets Manager seat limit, no need to adjust seats", exception.Message);
    }


    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ValidInput_Passes(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 300,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var additionalSeats = (organization.SmSeats + organizationUpdate.SeatAdjustment) - plan.BaseSeats;
        var additionalServiceAccounts = (organization.SmServiceAccounts + organizationUpdate.ServiceAccountsAdjustment) - plan.BaseServiceAccount;
        await sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate);

        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustSeatsAsync(organization, plan, additionalSeats.GetValueOrDefault());

        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustServiceAccountsAsync(organization, plan, additionalServiceAccounts.GetValueOrDefault());
    }

}
