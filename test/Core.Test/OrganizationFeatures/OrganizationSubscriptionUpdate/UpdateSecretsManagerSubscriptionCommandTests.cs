using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
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
            MaxAutoscaleSmSeats = null,
            SmSeatsAdjustment = 0
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
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmSeats = 1
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
            MaxAutoscaleSmSeats = 10,
            SmSeatsAdjustment = 15,
            SmSeats = organization.SmSeats.GetValueOrDefault() + 10,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() + 10) - plan.BaseSeats,
            SmServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 5,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + 5) - (int)plan.BaseServiceAccount
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 10,
            SmServiceAccountsAdjustment = 11,
            SmSeats = organization.SmSeats.GetValueOrDefault() + 1,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() + 1) - plan.BaseSeats,
            SmServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 11,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + 11) - (int)plan.BaseServiceAccount
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 15,
            SmServiceAccountsAdjustment = 1
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 15,
            SmServiceAccountsAdjustment = 1
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 15,
            SmServiceAccountsAdjustment = 1
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 1
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 1
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 0,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 1
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
        const int organizationServiceAccounts = 200;
        const int seatAdjustment = 5;
        const int maxAutoscaleSeats = 15;
        const int serviceAccountAdjustment = 100;
        const int maxAutoScaleServiceAccounts = 300;

        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            MaxAutoscaleSmSeats = 20,
            SmServiceAccounts = organizationServiceAccounts,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == organization.PlanType);
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            SmSeatsAdjustment = seatAdjustment,
            SmSeats = organization.SmSeats.GetValueOrDefault() + seatAdjustment,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() + seatAdjustment) - plan.BaseSeats,
            MaxAutoscaleSmSeats = maxAutoscaleSeats,

            SmServiceAccountsAdjustment = serviceAccountAdjustment,
            SmServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + serviceAccountAdjustment,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + serviceAccountAdjustment) - (int)plan.BaseServiceAccount,
            MaxAutoscaleSmServiceAccounts = maxAutoScaleServiceAccounts,

            MaxAutoscaleSmSeatsChanged = maxAutoscaleSeats != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            MaxAutoscaleSmServiceAccountsChanged = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        await sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate);

        if (organizationUpdate.SmSeatsAdjustment != 0)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .AdjustServiceAccountsAsync(organization, plan, organizationUpdate.SmServiceAccountsExcludingBase);

            // TODO: call ReferenceEventService - see AC-1481

            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.SmSeats == organizationUpdate.SmSeats));
        }

        if (organizationUpdate.SmServiceAccountsAdjustment != 0)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .AdjustSeatsAsync(organization, plan, organizationUpdate.SmSeatsExcludingBase);

            // TODO: call ReferenceEventService - see AC-1481

            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.SmServiceAccounts == (organizationServiceAccounts + organizationUpdate.SmServiceAccountsAdjustment)));
        }

        if (organizationUpdate.MaxAutoscaleSmSeats != organization.MaxAutoscaleSmSeats)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
               Arg.Is<Organization>(org =>
                   org.MaxAutoscaleSmSeats == organizationUpdate.MaxAutoscaleSmServiceAccounts));
        }

        if (organizationUpdate.MaxAutoscaleSmServiceAccounts != organization.MaxAutoscaleSmServiceAccounts)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.MaxAutoscaleSmServiceAccounts == organizationUpdate.MaxAutoscaleSmServiceAccounts));
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
            MaxAutoscaleSmSeats = 4,
            SmSeatsAdjustment = 1,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 5,
            SmSeats = organization.SmSeats.GetValueOrDefault() + 1,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() + 1) - plan.BaseSeats,
            SmServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 5,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + 5) - (int)plan.BaseServiceAccount
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
            MaxAutoscaleSmSeats = 7,
            SmSeatsAdjustment = -3,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 5,
            SmSeats = organization.SmSeats.GetValueOrDefault() - 3,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() - 3) - plan.BaseSeats,
            SmServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 5,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + 5) - (int)plan.BaseServiceAccount
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organizationId).Returns(8);


        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Your organization currently has 8 Secrets Manager seats. Your plan only allows 7 Secrets Manager seats. Remove some Secrets Manager users", exception.Message);
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
            MaxAutoscaleSmSeats = 21,
            SmSeatsAdjustment = 10,
            MaxAutoscaleSmServiceAccounts = 250,
            SmServiceAccountsAdjustment = 1,
            SmSeats = organization.SmSeats.GetValueOrDefault() + 10,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() + 10) - plan.BaseSeats,
            SmServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + 1,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + 1) - (int)plan.BaseServiceAccount
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 0,
            MaxAutoscaleSmServiceAccounts = 200,
            SmServiceAccountsAdjustment = 0,
            MaxAutoscaleSmSeatsChanged = 15 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            MaxAutoscaleSmServiceAccountsChanged = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
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
            MaxAutoscaleSmSeats = 1,
            SmSeatsAdjustment = 0,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 0,
            MaxAutoscaleSmSeatsChanged = 1 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            MaxAutoscaleSmServiceAccountsChanged = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
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
            MaxAutoscaleSmSeats = null,
            SmSeatsAdjustment = 0,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 0,
            MaxAutoscaleSmSeatsChanged = false,
            MaxAutoscaleSmServiceAccountsChanged = 300 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
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
            MaxAutoscaleSmSeats = 15,
            SmSeatsAdjustment = 5,
            MaxAutoscaleSmServiceAccounts = 300,
            SmServiceAccountsAdjustment = 100,
            SmSeats = organization.SmSeats.GetValueOrDefault() + 5,
            SmSeatsExcludingBase = (organization.SmSeats.GetValueOrDefault() + 5) - plan.BaseSeats,
            SmServiceAccounts = 300,
            SmServiceAccountsExcludingBase = (organization.SmServiceAccounts.GetValueOrDefault() + 100) - (int)plan.BaseServiceAccount,
            MaxAutoscaleSmSeatsChanged = 15 != organization.MaxAutoscaleSeats.GetValueOrDefault(),
            MaxAutoscaleSmServiceAccountsChanged = 200 != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault()
        };
        var currentServiceAccounts = 301;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your organization currently has 301 Service Accounts. Your plan only allows 300 Service Accounts. Remove some Service Accounts", exception.Message);
        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1).GetServiceAccountCountByOrganizationIdAsync(organization.Id);
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
