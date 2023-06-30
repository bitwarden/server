using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.ServiceAccounts;

[SutProviderCustomize]
public class AutoscaleServiceAccountsCommandTests
{
    [Theory, BitAutoData]
    public async Task AutoscaleServiceAccountsAsync_WithNonExistentOrganizationId_ThrowsNotFoundException(
        Guid organizationId,
        int serviceAccountSlotsToAdd,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organizationId, serviceAccountSlotsToAdd));
    }

    [Theory, BitAutoData]
    public async Task AutoscaleServiceAccountsAsync_WithNullSmServiceAccounts_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountSlotsToAdd,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.SmServiceAccounts = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd));
        Assert.Contains("Organization has no Secrets Manager Service Accounts limit", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task AutoscaleServiceAccountsAsync_WithNullGatewayCustomerId_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountSlotsToAdd,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.GatewayCustomerId = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd));
        Assert.Contains("No payment method found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task AutoscaleServiceAccountsAsync_WithNullGatewaySubscriptionId_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountSlotsToAdd,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd));
        Assert.Contains("No subscription found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData(PlanType.Custom)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    public async Task AutoscaleServiceAccountsAsync_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        int serviceAccountSlotsToAdd,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.PlanType = planType;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd));
        Assert.Contains("Existing plan not found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task AutoscaleServiceAccountsAsync_WithFreePlanType_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountSlotsToAdd,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.PlanType = PlanType.Free;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd));
        Assert.Contains("Plan does not allow additional service accounts", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task AutoscaleServiceAccountsAsync_WithLessThanPlanMinimumServiceAccounts_ThrowsBadRequestException(
        PlanType planType,
        Organization organization,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.PlanType = planType;
        organization.SmServiceAccounts = 1;

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, 1));
        Assert.Contains($"Plan has a minimum of {plan.BaseServiceAccount} service account slots.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory, BitAutoData(5, 50, 55)]
    public async Task AutoscaleServiceAccountsAsync_WithX_ThrowsBadRequestException(
        int serviceAccountSlotsToAdd,
        int organizationSmServiceAccounts,
        int currentServiceAccounts,
        Organization organization,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        var expectedServiceAccounts = organizationSmServiceAccounts + serviceAccountSlotsToAdd;

        organization.PlanType = PlanType.TeamsAnnually;
        organization.SmServiceAccounts = organizationSmServiceAccounts;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd));
        Assert.Contains($"Your organization currently has {currentServiceAccounts} service account slots filled. " +
                        $"Your new plan only has ({expectedServiceAccounts}) slots. Remove some service accounts.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [BitAutoData(50, 60, 50, 1)]
    [BitAutoData(50, 60, 50, 10)]
    public async Task AutoscaleServiceAccountsAsync_Success(
        int organizationSmServiceAccounts,
        int organizationMaxAutoscaleSmServiceAccounts,
        int currentServiceAccounts,
        int serviceAccountSlotsToAdd,
        Organization organization,
        SutProvider<AutoscaleServiceAccountsCommand> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;
        organization.SmServiceAccounts = organizationSmServiceAccounts;
        organization.MaxAutoscaleSmServiceAccounts = organizationMaxAutoscaleSmServiceAccounts;

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);
        var expectedResult = CoreHelpers.RandomString(10);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(currentServiceAccounts);

        sutProvider.GetDependency<IPaymentService>()
            .AdjustSeatsAsync(organization, plan, serviceAccountSlotsToAdd)
            .Returns(expectedResult);

        var result = await sutProvider.Sut.AutoscaleServiceAccountsAsync(organization.Id, serviceAccountSlotsToAdd);

        Assert.Equal(expectedResult, result);

        await sutProvider.GetDependency<IServiceAccountRepository>()
            .Received(1).GetServiceAccountCountByOrganizationIdAsync(organization.Id);
        await sutProvider.GetDependency<IPaymentService>()
            .Received(1).AdjustSeatsAsync(organization, plan, serviceAccountSlotsToAdd);
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r =>
            r.Type == ReferenceEventType.AdjustServiceAccounts &&
            r.Id == organization.Id &&
            r.Source == ReferenceEventSource.Organization));
        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(organization);
    }
}
