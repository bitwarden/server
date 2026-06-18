using Bit.Core.Platform.Push;
using Bit.Commercial.Pam.Services;
using Bit.Core.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Services;

[SutProviderCustomize]
public class ApproverInboxNotifierTests
{
    [Theory, BitAutoData]
    public async Task NotifyCollectionApproversAsync_PushesToEachManager(
        SutProvider<ApproverInboxNotifier> sutProvider, Guid collectionId, Guid userA, Guid userB)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManagingUserIdsAsync(collectionId)
            .Returns(new List<Guid> { userA, userB });

        await sutProvider.Sut.NotifyCollectionApproversAsync(collectionId);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushRefreshApproverInboxAsync(userA);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushRefreshApproverInboxAsync(userB);
    }

    [Theory, BitAutoData]
    public async Task NotifyCollectionApproversAsync_NoManagers_PushesNothing(
        SutProvider<ApproverInboxNotifier> sutProvider, Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManagingUserIdsAsync(collectionId)
            .Returns(new List<Guid>());

        await sutProvider.Sut.NotifyCollectionApproversAsync(collectionId);

        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushRefreshApproverInboxAsync(default);
    }
}
