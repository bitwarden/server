using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class AdjustServiceAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_ValidAdjustment_NoExceptions(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalServiceAccountOption = true;
        plan.BaseServiceAccount = 5;
        plan.MaxAdditionalServiceAccount = Int16.MaxValue;

        var occupiedServiceAccounts = 2;

        organization.SmServiceAccounts = 3;

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(occupiedServiceAccounts);

        sutProvider.GetDependency<IPaymentService>()
            .AdjustServiceAccountsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>(), Arg.Any<DateTime?>())
            .Returns("paymentIntentClientSecret");

        var result = await sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment);

        Assert.NotNull(result);
        Assert.Equal("paymentIntentClientSecret", result);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NoServiceAccountLimit_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.SmServiceAccounts = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NoPaymentMethod_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.GatewayCustomerId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NoSubscription_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_PlanDoesNotAllowAdditionalServiceAccount_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalServiceAccountOption = false;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_NegativeServiceAccountCount_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        organization.SmServiceAccounts = -5;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_ExceedsMaxAdditionalServiceAccounts_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalServiceAccountOption = true;
        plan.BaseServiceAccount = 5;
        plan.MaxAdditionalServiceAccount = 10;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_OccupiedServiceAccountsExceedsNewCount_ThrowsBadRequestException(
        Organization organization,
        int serviceAccountAdjustment,
        int occupiedServiceAccounts,
        SutProvider<AdjustServiceAccountsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalServiceAccountOption = true;
        plan.BaseServiceAccount = 5;
        plan.MaxAdditionalServiceAccount = 10;

        organization.SmServiceAccounts = 10;

        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetServiceAccountCountByOrganizationIdAsync(organization.Id)
            .Returns(occupiedServiceAccounts);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustServiceAccountsAsync(organization, serviceAccountAdjustment));
    }
}
