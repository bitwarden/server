using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class UpdateSeatsAutoscalingCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateSeatsAutoscalingAsync_BelowCurrentCount_ThrowsBadRequestException(
        Organization organization,
        int? maxAutoscaleSeats,
        SutProvider<UpdateSeatsAutoscalingCommand> sutProvider)
    {
        organization.SmSeats = 10;
        maxAutoscaleSeats = 5;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateSeatsAutoscalingAsync(organization, maxAutoscaleSeats));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSeatsAutoscalingAsync_ValidInput_UpdatesOrganization(
        Organization organization,
        int? maxAutoscaleSeats,
        SutProvider<UpdateSeatsAutoscalingCommand> sutProvider)
    {
        maxAutoscaleSeats = 5;
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.MaxUsers = 20;
        maxAutoscaleSeats = 15;
        organization.SmSeats = 10;

        await sutProvider.Sut.UpdateSeatsAutoscalingAsync(organization, maxAutoscaleSeats);

        Assert.Equal(maxAutoscaleSeats, organization.MaxAutoscaleSmSeats);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .ReplaceAndUpdateCacheAsync(organization);
    }
}
