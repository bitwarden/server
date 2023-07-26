using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
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
    public async Task UpdateSubscriptionAsync_NoOrganization_Throws(
        SecretsManagerSubscriptionUpdate secretsManagerSubscriptionUpdate,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        secretsManagerSubscriptionUpdate.Organization = null;

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(secretsManagerSubscriptionUpdate));

        Assert.Contains("Organization is not found", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_NoSecretsManagerAccess_ThrowsException(
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            SmSeats = 10,
            SmServiceAccounts = 5,
            UseSecretsManager = false,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10
        };

        var secretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, seatAdjustment: 0, maxAutoscaleSeats: null, serviceAccountAdjustment: 0, maxAutoscaleServiceAccounts: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
             () => sutProvider.Sut.UpdateSubscriptionAsync(secretsManagerSubscriptionUpdate));

        Assert.Contains("Organization has no access to Secrets Manager.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_SeatsAdustmentGreaterThanMaxAutoscaleSeats_ThrowsException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 15, maxAutoscaleSeats: 10, serviceAccountAdjustment: 0, maxAutoscaleServiceAccounts: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ServiceAccountsGreaterThanMaxAutoscaleSeats_ThrowsException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 15, serviceAccountAdjustment: 11, maxAutoscaleServiceAccounts: 10);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("Cannot set max service accounts autoscaling below service account amount", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_NullGatewayCustomerId_ThrowsException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 15, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 15);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("No payment method found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_NullGatewaySubscriptionId_ThrowsException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 15, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 15);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("No subscription found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_OrgWithNullSmSeatOnSeatsAdjustment_ThrowsException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 15, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 15);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));

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
    public async Task UpdateSubscriptionAsync_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
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
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 15, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 300);
        organization.PlanType = planType;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("Existing plan not found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateSubscriptionAsync_WithHasAdditionalSeatsOptionfalse_ThrowsBadRequestException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 15, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 300);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("Plan does not allow additional Secrets Manager seats.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public async Task UpdateSubscriptionAsync_WithHasAdditionalServiceAccountOptionFalse_ThrowsBadRequestException(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 0, maxAutoscaleSeats: 15, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 300);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("Plan does not allow additional Service Accounts", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSubscriptionAsync_ValidInput_Passes(
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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization,
            seatAdjustment: seatAdjustment, maxAutoscaleSeats: maxAutoscaleSeats,
            serviceAccountAdjustment: serviceAccountAdjustment, maxAutoscaleServiceAccounts: maxAutoScaleServiceAccounts);

        await sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate);

        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSeatsAsync(organization, plan, organizationUpdate.SmSeatsExcludingBase);
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustServiceAccountsAsync(organization, plan, organizationUpdate.SmServiceAccountsExcludingBase);

        // TODO: call ReferenceEventService - see AC-1481

        AssertUpdatedOrganization(() => Arg.Is<Organization>(org =>
                org.Id == organizationId
                && org.SmSeats == organizationUpdate.SmSeats
                && org.MaxAutoscaleSmSeats == organizationUpdate.MaxAutoscaleSmSeats
                && org.SmServiceAccounts == (organizationServiceAccounts + serviceAccountAdjustment)
                && org.MaxAutoscaleSmServiceAccounts == organizationUpdate.MaxAutoscaleSmServiceAccounts), sutProvider);

        await sutProvider.GetDependency<IMailService>().Received(1).SendSecretsManagerMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, Arg.Any<IEnumerable<string>>());
        await sutProvider.GetDependency<IMailService>().Received(1).SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value, Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task UpdateSubscriptionAsync_ValidInput_WithNullMaxAutoscale_Passes(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        int organizationServiceAccounts = 200;
        int seatAdjustment = 5;
        int? maxAutoscaleSeats = null;
        int serviceAccountAdjustment = 100;
        int? maxAutoScaleServiceAccounts = null;

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
        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization,
            seatAdjustment: seatAdjustment, maxAutoscaleSeats: maxAutoscaleSeats,
            serviceAccountAdjustment: serviceAccountAdjustment, maxAutoscaleServiceAccounts: maxAutoScaleServiceAccounts);

        await sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate);

        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustSeatsAsync(organization, plan, organizationUpdate.SmSeatsExcludingBase);
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .AdjustServiceAccountsAsync(organization, plan, organizationUpdate.SmServiceAccountsExcludingBase);

        // TODO: call ReferenceEventService - see AC-1481

        AssertUpdatedOrganization(() => Arg.Is<Organization>(org =>
                org.Id == organizationId
                && org.SmSeats == organizationUpdate.SmSeats
                && org.MaxAutoscaleSmSeats == organizationUpdate.MaxAutoscaleSmSeats
                && org.SmServiceAccounts == (organizationServiceAccounts + serviceAccountAdjustment)
                && org.MaxAutoscaleSmServiceAccounts == organizationUpdate.MaxAutoscaleSmServiceAccounts), sutProvider);

        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendSecretsManagerMaxSeatLimitReachedEmailAsync(default, default, default);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowSeatCount(
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
        var update = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 1, maxAutoscaleSeats: 4, serviceAccountAdjustment: 5, maxAutoscaleServiceAccounts: 300);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSubscriptionAsync_ThrowsBadRequestException_WhenOccupiedSeatsExceedNewSeatTotal(
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
        var update = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: -3, maxAutoscaleSeats: 7, serviceAccountAdjustment: 5, maxAutoscaleServiceAccounts: 300);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organizationId).Returns(8);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
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
        var update = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 10, maxAutoscaleSeats: 21, serviceAccountAdjustment: 1, maxAutoscaleServiceAccounts: 250);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 0, maxAutoscaleSeats: 15, serviceAccountAdjustment: 0, maxAutoscaleServiceAccounts: 200);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 0, maxAutoscaleSeats: 1, serviceAccountAdjustment: 0, maxAutoscaleServiceAccounts: 300);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
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

        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 0, maxAutoscaleSeats: null, serviceAccountAdjustment: 0, maxAutoscaleServiceAccounts: 300);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
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
            MaxAutoscaleSmSeats = 20,
            SmServiceAccounts = 301,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var organizationUpdate = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 5, maxAutoscaleSeats: 15, serviceAccountAdjustment: -100, maxAutoscaleServiceAccounts: 300);
        var currentServiceAccounts = 301;

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(organizationUpdate));
        Assert.Contains("Your organization currently has 301 Service Accounts. Your plan only allows 201 Service Accounts. Remove some Service Accounts", exception.Message);
        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1).GetServiceAccountCountByOrganizationIdAsync(organization.Id);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task AdjustServiceAccountsAsync_WithEnterpriseOrTeamsPlans_Success(PlanType planType, Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == planType);

        var organizationSeats = plan.BaseSeats + 10;
        var organizationMaxAutoscaleSeats = 20;
        var organizationServiceAccounts = plan.BaseServiceAccount.GetValueOrDefault() + 10;
        var organizationMaxAutoscaleServiceAccounts = 300;

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
            UseSecretsManager = true,
            SmSeats = organizationSeats,
            MaxAutoscaleSmSeats = organizationMaxAutoscaleSeats,
            SmServiceAccounts = organizationServiceAccounts,
            MaxAutoscaleSmServiceAccounts = organizationMaxAutoscaleServiceAccounts
        };

        var smServiceAccountsAdjustment = 10;
        var expectedSmServiceAccounts = organizationServiceAccounts + smServiceAccountsAdjustment;
        var expectedSmServiceAccountsExcludingBase = expectedSmServiceAccounts - plan.BaseServiceAccount.GetValueOrDefault();

        await sutProvider.Sut.AutoAddServiceAccountsAsync(organization, smServiceAccountsAdjustment);

        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustServiceAccountsAsync(
            Arg.Is<Organization>(o => o.Id == organizationId),
            plan,
            expectedSmServiceAccountsExcludingBase);
        // TODO: call ReferenceEventService - see AC-1481
        AssertUpdatedOrganization(() => Arg.Is<Organization>(o =>
                o.Id == organizationId
                && o.SmSeats == organizationSeats
                && o.MaxAutoscaleSmSeats == organizationMaxAutoscaleSeats
                && o.SmServiceAccounts == expectedSmServiceAccounts
                && o.MaxAutoscaleSmServiceAccounts == organizationMaxAutoscaleServiceAccounts), sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task ServiceAccountAutoscaling_MaxLimitReached_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;
        organization.SmServiceAccounts = 9;
        organization.MaxAutoscaleSmServiceAccounts = 10;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustServiceAccounts(2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Secrets Manager service account limit has been reached.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task ServiceAccountAutoscaling_Subtracting_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustServiceAccounts(-2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot use autoscaling to subtract service accounts.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SmSeatAutoscaling_MaxLimitReached_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;
        organization.SmSeats = 9;
        organization.MaxAutoscaleSmSeats = 10;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustSeats(2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Secrets Manager seat limit has been reached.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SmSeatAutoscaling_Subtracting_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.UseSecretsManager = true;

        var update = new SecretsManagerSubscriptionUpdate(organization, true);
        update.AdjustSeats(-2);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains("Cannot use autoscaling to subtract service accounts.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(false, "Cannot update subscription on a self-hosted instance.")]
    [BitAutoData(true, "Cannot autoscale on a self-hosted instance;")]
    public async Task UpdatingSubscription_WhenSelfHosted_ThrowsBadRequestException(
        bool autoscaling,
        string expectedError,
        Organization organization,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UseSecretsManager = true;

        var update = new SecretsManagerSubscriptionUpdate(organization, autoscaling);
        update.AdjustSeats(2);

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscriptionAsync(update));
        Assert.Contains(expectedError, exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustServiceAccountsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        // TODO: call ReferenceEventService - see AC-1481
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationMaxSeatLimitReachedEmailAsync(Arg.Any<Organization>(), Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());

        sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        sutProvider.GetDependency<IApplicationCacheService>().DidNotReceiveWithAnyArgs().UpsertOrganizationAbilityAsync(default);
    }

    private void AssertUpdatedOrganization(Func<Organization> organizationMatcher, SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organizationMatcher());
        sutProvider.GetDependency<IApplicationCacheService>().Received(1).UpsertOrganizationAbilityAsync(organizationMatcher());
    }
}
