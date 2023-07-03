using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
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
    [BitAutoData(PlanType.Custom)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    public async Task UpdateSecretsManagerSubscription_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        PlanType planType,
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
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };




        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Existing plan not found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task UpdateSecretsManagerSubscription_WithHasAdditionalSeatsOptionfalse_ThrowsBadRequestException(
        PlanType planType,
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
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };




        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var plan = StaticStore.SecretManagerPlans.Single(x => x.Type == organization.PlanType);
        plan.HasAdditionalSeatsOption = false;
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Plan does not allow additional Secrets Manager seats.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }


    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task UpdateSecretsManagerSubscription_WithHasAdditionalServiceAccountOptionFalse_ThrowsBadRequestException(
        PlanType planType,
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
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };




        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var plan = StaticStore.SecretManagerPlans.Single(x => x.Type == organization.PlanType);
        plan.HasAdditionalServiceAccountOption = false;
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Plan does not allow additional Service Account.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }


    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task UpdateSecretsManagerSubscription_ValidInput_Passes(
        PlanType planType,
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
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var previousSeats = organization.SmSeats;
        var previousServiceAccounts = organization.SmServiceAccounts;
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
        plan.HasAdditionalSeatsOption = true;
        plan.HasAdditionalServiceAccountOption = true;
        var additionalSeats = (organization.SmSeats + organizationUpdate.SeatAdjustment) - plan.BaseSeats;
        var additionalServiceAccounts =
            (organization.SmServiceAccounts + organizationUpdate.ServiceAccountsAdjustment) - plan.BaseServiceAccount;
        var newSeatTotal = organization.SmSeats.HasValue
            ? organization.SmSeats.Value + organizationUpdate.SeatAdjustment
            : organizationUpdate.SeatAdjustment;

        var newServiceAccountsTotal = organization.SmServiceAccounts.HasValue
            ? organization.SmServiceAccounts.Value + organizationUpdate.ServiceAccountsAdjustment
            : organizationUpdate.ServiceAccountsAdjustment;

        await sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate);

        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSeatsAsync(organization, plan, additionalSeats.GetValueOrDefault());

        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustServiceAccountsAsync(organization, plan, additionalServiceAccounts.GetValueOrDefault());

        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(
            Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.AdjustSmSeats &&
                referenceEvent.Id == organization.Id &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.Seats == newSeatTotal &&
                referenceEvent.PreviousSeats == previousSeats
            )
        );

        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(
            Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.AdjustServiceAccounts &&
                referenceEvent.Id == organization.Id &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.ServiceAccounts == newServiceAccountsTotal &&
                referenceEvent.PreviousServiceAccounts == previousServiceAccounts
            )
        );

        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(organization);
    }
    
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task UpdateSecretsManagerSubscription_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowSeatCount(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 5,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 4,
            MaxAutoscaleSmServiceAccounts = 300,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var update = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 4,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5
        };

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
    }
    
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task UpdateSecretsManagerSubscription_ThrowsBadRequestException_WhenMaxSeatLimitExceeded(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
         var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            PlanType = planType,
            MaxAutoscaleSmSeats = 10
        };
         
         sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
             .Returns(organization);
         

         var update = new OrganizationUpdate
         {
             OrganizationId = organizationId,
             MaxAutoscaleSeats = 10,
             SeatAdjustment = 1
         };
         
         await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
    }


}
