using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class ExcludeCipherFromBrowserHealthCommandTests
{
    [Theory, BitAutoData]
    public async Task ExcludeAsync_WithoutPremium_ThrowsBadRequest_AndDoesNotWrite(
        SutProvider<ExcludeCipherFromBrowserHealthCommand> sutProvider, Guid cipherId, Guid userId)
    {
        sutProvider.GetDependency<IHasPremiumAccessQuery>().HasPremiumAccessAsync(userId).Returns(false);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ExcludeAsync(cipherId, userId));

        await sutProvider.GetDependency<ICipherBrowserHealthExclusionRepository>()
            .DidNotReceiveWithAnyArgs().ExcludeAsync(default!, default);
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs().PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task ExcludeAsync_NothingExcluded_ThrowsNotFound_AndDoesNotPush(
        SutProvider<ExcludeCipherFromBrowserHealthCommand> sutProvider, Guid cipherId, Guid userId)
    {
        sutProvider.GetDependency<IHasPremiumAccessQuery>().HasPremiumAccessAsync(userId).Returns(true);
        sutProvider.GetDependency<ICipherBrowserHealthExclusionRepository>()
            .ExcludeAsync(Arg.Any<IEnumerable<Guid>>(), userId).Returns(0);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ExcludeAsync(cipherId, userId));

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs().PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task ExcludeAsync_Excluded_PushesCipherSyncExcludingCurrentContext(
        SutProvider<ExcludeCipherFromBrowserHealthCommand> sutProvider, Guid cipherId, Guid userId)
    {
        sutProvider.GetDependency<IHasPremiumAccessQuery>().HasPremiumAccessAsync(userId).Returns(true);
        sutProvider.GetDependency<ICipherBrowserHealthExclusionRepository>()
            .ExcludeAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == cipherId), userId).Returns(1);

        await sutProvider.Sut.ExcludeAsync(cipherId, userId);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n =>
                n.Type == PushType.SyncCiphers && n.TargetId == userId && n.ExcludeCurrentContext));
    }
}
