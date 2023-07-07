using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
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

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Organization is not found", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
        await VerifyDependencyNotCalledAsync(sutProvider);
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
            MaxAutoscaleSmServiceAccounts = 10,
            PlanType = PlanType.EnterpriseAnnually,
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 10,
            SeatAdjustment = 15,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() + 10,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 10) - plan.BaseSeats,
            NewTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 5,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 5) - (int)plan.BaseServiceAccount
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "9"
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 10,
            ServiceAccountsAdjustment = 11,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() + 1,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 1) - plan.BaseSeats,
            NewTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 11,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 11) - (int)plan.BaseServiceAccount
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Cannot set max Service Accounts autoscaling below Service Accounts count", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("No payment method found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("No subscription found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Organization has no Secrets Manager seat limit, no need to adjust seats", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Existing plan not found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Plan does not allow additional Secrets Manager seats.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 0,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Plan does not allow additional Service Accounts", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
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
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() + 5,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 5) - plan.BaseSeats,
            NewTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 100,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 100) - (int)plan.BaseServiceAccount,
            AutoscaleSeatAdjustmentRequired = 15 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            AutoscaleServiceAccountAdjustmentRequired = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        await sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate);
        if (organizationUpdate.ServiceAccountsAdjustment != 0)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .AdjustSeatsAsync(organization, plan, organizationUpdate.NewAdditionalSeats);

            // TODO: call ReferenceEventService - see AC-1481
        }

        if (organizationUpdate.SeatAdjustment != 0)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .AdjustServiceAccountsAsync(organization, plan, organizationUpdate.NewAdditionalServiceAccounts);

            // TODO: call ReferenceEventService - see AC-1481
        }

        if (organizationUpdate.SeatAdjustment != 0)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.SmSeats == organizationUpdate.NewTotalSeats));
        }

        if (organizationUpdate.ServiceAccountsAdjustment != 0)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.SmServiceAccounts == organizationUpdate.NewTotalServiceAccounts));
        }

        if (organizationUpdate.MaxAutoscaleSeats != organization.MaxAutoscaleSmSeats)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
               Arg.Is<Organization>(org =>
                   org.MaxAutoscaleSmSeats == organizationUpdate.MaxAutoscaleServiceAccounts));
        }

        if (organizationUpdate.MaxAutoscaleServiceAccounts != organization.MaxAutoscaleSmServiceAccounts)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.MaxAutoscaleSmServiceAccounts == organizationUpdate.MaxAutoscaleServiceAccounts));
        }

        await sutProvider.GetDependency<IMailService>().Received(1).SendSecretsManagerMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, Arg.Any<IEnumerable<string>>());
        await sutProvider.GetDependency<IMailService>().Received(1).SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value, Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowSeatCount(
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
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 4,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() + 1,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 1) - plan.BaseSeats,
            NewTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 5,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 5) - (int)plan.BaseServiceAccount
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
            GatewaySubscriptionId = "2",
            PlanType = PlanType.EnterpriseAnnually
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 7,
            SeatAdjustment = -3,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() - 3,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() - 3) - plan.BaseSeats,
            NewTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 5,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 5) - (int)plan.BaseServiceAccount
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organizationId).Returns(8);


        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Your organization currently has 8 Secrets Manager seats. Your plan only allows (7) Secrets Manager seats. Remove some Secrets Manager users", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
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
            SmServiceAccounts = null,
            PlanType = PlanType.EnterpriseAnnually
        };
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 21,
            SeatAdjustment = 10,
            MaxAutoscaleServiceAccounts = 250,
            ServiceAccountsAdjustment = 1,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() + 10,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 10) - plan.BaseSeats,
            NewTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 1,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 1) - (int)plan.BaseServiceAccount
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Organization has no Service Accounts limit, no need to adjust Service Accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task AutoscaleSeatsAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsExceedPlanMaxUsers(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 3,
            UseSecretsManager = true,
            SmServiceAccounts = 100,
            PlanType = PlanType.Free,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
        };

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 0,
            MaxAutoscaleServiceAccounts = 200,
            ServiceAccountsAdjustment = 0,
            AutoscaleSeatAdjustmentRequired = 15 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            AutoscaleServiceAccountAdjustmentRequired = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan has a Secrets Manager seat limit of 2, but you have specified a max autoscale count of 15.Reduce your max autoscale count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task AutoscaleSeatsAsync_ThrowsBadRequestException_WhenPlanDoesNotAllowSeatAutoscale(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 1,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 1,
            SeatAdjustment = 0,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 0,
            AutoscaleSeatAdjustmentRequired = 1 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            AutoscaleServiceAccountAdjustmentRequired = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan does not allow Secrets Manager seat autoscaling", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);

    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateServiceAccountAutoscaling_ThrowsBadRequestException_WhenPlanDoesNotAllowServiceAccountAutoscale(
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
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = null,
            SeatAdjustment = 0,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 0,
            AutoscaleSeatAdjustmentRequired = false,
            AutoscaleServiceAccountAdjustmentRequired = 300 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan does not allow Service Accounts autoscaling.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);

    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateServiceAccountAutoscaling_WhenCurrentServiceAccountsIsGreaterThanNew_ThrowsBadRequestException(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 301,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100,
            NewTotalSeats = organization.SmSeats.GetValueOrDefault() + 5,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 5) - plan.BaseSeats,
            NewTotalServiceAccounts = 300,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 100) - (int)plan.BaseServiceAccount,
            AutoscaleSeatAdjustmentRequired = 15 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            AutoscaleServiceAccountAdjustmentRequired = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };
        var currentServiceAccounts = 301;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your organization currently has 301 Service Accounts. Your plan only allows (300) Service Accounts. Remove some Service Accounts", exception.Message);
        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1).GetServiceAccountCountByOrganizationIdAsync(organization.Id);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateServiceAccountAutoscaling_WhenCurrentSeatsIsGreaterThanNew_ThrowsBadRequestException(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 301,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100,
            NewTotalSeats = 9,
            NewAdditionalSeats = (organization.SmSeats.GetValueOrDefault() + 5) - plan.BaseSeats,
            NewTotalServiceAccounts = 300,
            NewAdditionalServiceAccounts = (organization.SmServiceAccounts.GetValueOrDefault() + 100) - (int)plan.BaseServiceAccount,
            AutoscaleSeatAdjustmentRequired = 15 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            AutoscaleServiceAccountAdjustmentRequired = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };
        var currentSeats = 10;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(currentSeats);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your organization currently has 10 Secrets Manager seats. Your plan only allows (9) Secrets Manager seats. Remove some Secrets Manager users.", exception.Message);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustServiceAccountsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        // TODO: call ReferenceEventService - see AC-1481
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationMaxSeatLimitReachedEmailAsync(Arg.Any<Organization>(), Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());
    }
}
