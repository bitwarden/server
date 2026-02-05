using Bit.Api.AdminConsole.Jobs;
using Bit.Core;
using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Quartz;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Jobs;

[SutProviderCustomize]
public class OrganizationSubscriptionUpdateJobTests
{
    [Theory]
    [BitAutoData]
    public async Task ExecuteJobAsync_WhenScimInviteUserIsDisabled_ThenQueryAndCommandAreNotExecuted(
        SutProvider<OrganizationSubscriptionUpdateJob> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization)
            .Returns(false);

        var contextMock = Substitute.For<IJobExecutionContext>();

        await sutProvider.Sut.Execute(contextMock);

        await sutProvider.GetDependency<IGetOrganizationSubscriptionsToUpdateQuery>()
            .DidNotReceive()
            .GetOrganizationSubscriptionsToUpdateAsync();

        await sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .DidNotReceive()
            .UpdateOrganizationSubscriptionAsync(Arg.Any<IEnumerable<OrganizationSubscriptionUpdate>>());
    }

    [Theory]
    [BitAutoData]
    public async Task ExecuteJobAsync_WhenScimInviteUserIsEnabled_ThenQueryAndCommandAreExecuted(
        SutProvider<OrganizationSubscriptionUpdateJob> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization)
            .Returns(true);

        var contextMock = Substitute.For<IJobExecutionContext>();

        await sutProvider.Sut.Execute(contextMock);

        await sutProvider.GetDependency<IGetOrganizationSubscriptionsToUpdateQuery>()
            .Received(1)
            .GetOrganizationSubscriptionsToUpdateAsync();

        await sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Received(1)
            .UpdateOrganizationSubscriptionAsync(Arg.Any<IEnumerable<OrganizationSubscriptionUpdate>>());
    }
}
