#nullable enable
using Bit.Api.NotificationCenter.Controllers;
using Bit.Api.NotificationCenter.Models.Request;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.NotificationCenter.Controllers;

[ControllerCustomize(typeof(NotificationsController))]
[SutProviderCustomize]
public class NotificationsControllerTest
{
    [Theory]
    [BitAutoData]
    [NotificationListCustomize(20)]
    public async Task List_DefaultFilter_ReturnedMatchingNotifications(SutProvider<NotificationsController> sutProvider,
        List<Notification> notifications)
    {
        sutProvider.GetDependency<IGetNotificationsForUserQuery>()
            .GetByUserIdStatusFilterAsync(Arg.Any<NotificationStatusFilter>())
            .Returns(notifications);

        var expectedNotificationGroupedById = notifications
            .Take(10)
            .ToDictionary(n => n.Id);

        var filter = new NotificationFilterRequestModel();

        var listResponse = await sutProvider.Sut.List(filter);

        Assert.Equal(10, listResponse.Data.Count());
        Assert.All(listResponse.Data, notificationResponseModel =>
        {
            var expectedNotification = expectedNotificationGroupedById[notificationResponseModel.Id];
            Assert.NotNull(expectedNotification);
            Assert.Equal(expectedNotification.Id, notificationResponseModel.Id);
            Assert.Equal(expectedNotification.Priority, notificationResponseModel.Priority);
            Assert.Equal(expectedNotification.Title, notificationResponseModel.Title);
            Assert.Equal(expectedNotification.Body, notificationResponseModel.Body);
            Assert.Equal(expectedNotification.RevisionDate, notificationResponseModel.Date);
            Assert.Equal("notification", notificationResponseModel.Object);
        });
        Assert.Null(listResponse.ContinuationToken);
        Assert.Equal("list", listResponse.Object);
    }
}
