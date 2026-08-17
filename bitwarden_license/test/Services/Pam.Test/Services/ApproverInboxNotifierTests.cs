using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

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

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(p =>
                p.Type == PushType.RefreshApproverInbox && p.TargetId == userA));
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(p =>
                p.Type == PushType.RefreshApproverInbox && p.TargetId == userB));
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
            .PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }
}
