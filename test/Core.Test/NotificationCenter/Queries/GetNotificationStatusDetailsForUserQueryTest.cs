#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationCenter.Queries;

[SutProviderCustomize]
[NotificationStatusDetailsCustomize]
public class GetNotificationStatusDetailsForUserQueryTest
{
    private static void Setup(
        SutProvider<GetNotificationStatusDetailsForUserQuery> sutProvider,
        List<NotificationStatusDetails> notificationsStatusDetails,
        NotificationStatusFilter statusFilter,
        Guid? userId
    )
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider
            .GetDependency<INotificationRepository>()
            .GetByUserIdAndStatusAsync(
                userId.GetValueOrDefault(Guid.NewGuid()),
                Arg.Any<ClientType>(),
                statusFilter
            )
            .Returns(notificationsStatusDetails);
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_NotLoggedIn_NotFoundException(
        SutProvider<GetNotificationStatusDetailsForUserQuery> sutProvider,
        List<NotificationStatusDetails> notificationsStatusDetails,
        NotificationStatusFilter notificationStatusFilter
    )
    {
        Setup(sutProvider, notificationsStatusDetails, notificationStatusFilter, userId: null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetByUserIdStatusFilterAsync(notificationStatusFilter)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task GetByUserIdStatusFilterAsync_NotificationsFound_Returned(
        SutProvider<GetNotificationStatusDetailsForUserQuery> sutProvider,
        List<NotificationStatusDetails> notificationsStatusDetails,
        NotificationStatusFilter notificationStatusFilter
    )
    {
        Setup(sutProvider, notificationsStatusDetails, notificationStatusFilter, Guid.NewGuid());

        var actualNotificationsStatusDetails = await sutProvider.Sut.GetByUserIdStatusFilterAsync(
            notificationStatusFilter
        );

        Assert.Equal(notificationsStatusDetails, actualNotificationsStatusDetails);
    }
}
