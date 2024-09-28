#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.Queries;

using Bit.Core.NotificationCenter.Entities;
using NSubstitute;
using Xunit;

[SutProviderCustomize]
[NotificationCustomize]
public class GetNotificationsForUserQueryTest
{
    private static void Setup(SutProvider<GetNotificationsForUserQuery> sutProvider,
        List<Notification> notifications, NotificationStatusFilter statusFilter, Guid? userId)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<INotificationRepository>().GetByUserIdAndStatusAsync(
                userId.GetValueOrDefault(Guid.NewGuid()), Arg.Any<ClientType>(), statusFilter)
            .Returns(notifications);
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_NotLoggedIn_NotFoundException(
        SutProvider<GetNotificationsForUserQuery> sutProvider,
        List<Notification> notifications, NotificationStatusFilter notificationStatusFilter)
    {
        Setup(sutProvider, notifications, notificationStatusFilter, userId: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByUserIdStatusFilterAsync(notificationStatusFilter));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_NotificationsFound_Returned(
        SutProvider<GetNotificationsForUserQuery> sutProvider,
        List<Notification> notifications, NotificationStatusFilter notificationStatusFilter)
    {
        Setup(sutProvider, notifications, notificationStatusFilter, Guid.NewGuid());

        var actualNotifications = await sutProvider.Sut.GetByUserIdStatusFilterAsync(notificationStatusFilter);

        Assert.Equal(notifications, actualNotifications);
    }
}
