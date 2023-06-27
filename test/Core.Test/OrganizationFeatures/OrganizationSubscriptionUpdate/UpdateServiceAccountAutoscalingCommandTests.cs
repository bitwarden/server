using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;
[SutProviderCustomize]
public class UpdateServiceAccountAutoscalingCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateServiceAccountAutoscalingAsync_BelowCurrentCount_ThrowsBadRequestException(
        Organization organization,
        int? maxAutoscaleServiceAccounts,
        SutProvider<UpdateServiceAccountAutoscalingCommand> sutProvider)
    {
        organization.SmServiceAccounts = 10;
        maxAutoscaleServiceAccounts = 5;
    
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateServiceAccountAutoscalingAsync(organization, maxAutoscaleServiceAccounts));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateServiceAccountAutoscalingAsync_SeatAutoscaleNotAllowed_ThrowsBadRequestException(
        Organization organization,
        int? maxAutoscaleServiceAccounts,
        SutProvider<UpdateServiceAccountAutoscalingCommand> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;
        var plan = StaticStore.GetSecretsManagerPlan(organization.PlanType);
        plan.AllowServiceAccountsAutoscale = false;
    
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateServiceAccountAutoscalingAsync(organization, maxAutoscaleServiceAccounts));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateServiceAccountAutoscalingAsync_ExceedsMaxServiceAccountLimit_ThrowsBadRequestException(
        Organization organization,
        int? maxAutoscaleServiceAccounts,
        SutProvider<UpdateServiceAccountAutoscalingCommand> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;
        var plan = StaticStore.GetSecretsManagerPlan(organization.PlanType);
        
        plan.MaxServiceAccounts = 20;
    
        maxAutoscaleServiceAccounts = 25;
    
        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateServiceAccountAutoscalingAsync(organization, maxAutoscaleServiceAccounts));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateServiceAccountAutoscalingAsync_ValidInput_UpdatesOrganization(
        Organization organization,
        int? maxAutoscaleServiceAccounts,
        SutProvider<UpdateServiceAccountAutoscalingCommand> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        var plan = StaticStore.GetSecretsManagerPlan(organization.PlanType);
        plan.MaxServiceAccounts = (short?)maxAutoscaleServiceAccounts.GetValueOrDefault();
        organization.SmServiceAccounts = (short?)maxAutoscaleServiceAccounts.GetValueOrDefault();

        await sutProvider.Sut.UpdateServiceAccountAutoscalingAsync(organization, maxAutoscaleServiceAccounts);

        Assert.Equal(maxAutoscaleServiceAccounts, organization.MaxAutoscaleSmServiceAccounts);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .ReplaceAndUpdateCacheAsync(organization);
    }
}
