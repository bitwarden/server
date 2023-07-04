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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
    public async Task UpdateSecretsManagerSubscription_NoSecretsManagerAccess_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            UseSecretsManager = false,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            SeatAdjustment = 1,
            MaxAutoscaleSeats = 1
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
             () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Organization has no access to Secrets Manager.", exception.Message);
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
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "9"
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 10,
            ServiceAccountsAdjustment = 11
        };

        var plan = StaticStore.SecretManagerPlans.Single(x => x.Type == organization.PlanType);
        plan.HasAdditionalSeatsOption = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Cannot set max Service Accounts autoscaling below Service Accounts count", exception.Message);
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
            UseSecretsManager = true,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1"
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1"
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
            UseSecretsManager = true,
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
            UseSecretsManager = true,
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
            UseSecretsManager = true,
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
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
        Assert.Contains("Plan does not allow additional Service Accounts", exception.Message, StringComparison.InvariantCultureIgnoreCase);
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
            UseSecretsManager = true,
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100
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

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, Arg.Any<IEnumerable<string>>());

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value, Arg.Any<IEnumerable<string>>());
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
            UseSecretsManager = true,
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

        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 4,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ThrowsBadRequestException_WhenOccupiedSeatsExceedNewSeatTotal(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        plan.BaseSeats = 5;
        plan.HasAdditionalSeatsOption = true;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organizationId).Returns(8);

        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 7,
            SeatAdjustment = -3,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Your organization currently has 8 Secrets Manager seats. Your new plan only allows (7) Secrets Manager seats. Remove some Secrets Manager users", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_ThrowsBadRequestException_WhenAdditionalSeatsExceedMaxAdditionalSeats(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        plan.BaseSeats = 5;
        plan.MaxAdditionalSeats = 2;
        plan.HasAdditionalSeatsOption = true;

        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 21,
            SeatAdjustment = 10
        };
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Organization plan allows a maximum of 2 additional Secrets Manager seats.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountsAsync_ThrowsBadRequestException_WhenSmServiceAccountsIsNull(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            UseSecretsManager = true,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
            SmServiceAccounts = null
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        plan.HasAdditionalSeatsOption = true;
        plan.AllowSeatAutoscale = true;
        plan.AllowServiceAccountsAutoscale = true;

        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 21,
            SeatAdjustment = 10,
            MaxAutoscaleServiceAccounts = 250,
            ServiceAccountsAdjustment = 1
        };
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Organization has no Service Accounts limit, no need to adjust Service Accounts", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AutoscaleSeatsAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsExceedPlanMaxUsers(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 3,
            UseSecretsManager = true,
            SmServiceAccounts = 100,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);

        plan.AllowSeatAutoscale = true;
        plan.MaxUsers = 10;
        plan.HasAdditionalSeatsOption = true;
        plan.HasAdditionalServiceAccountOption = true;

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 200,
            ServiceAccountsAdjustment = 100
        };


        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan has a Secrets Manager seat limit of 10, but you have specified a max autoscale count of 15.Reduce your max autoscale count.", exception.Message);
    }

}
