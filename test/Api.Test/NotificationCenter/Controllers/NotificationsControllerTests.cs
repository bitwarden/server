#nullable enable
using Bit.Api.NotificationCenter.Controllers;
using Bit.Api.NotificationCenter.Models.Request;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Models.Data;
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
public class NotificationsControllerTests
{
    [Theory]
    [BitAutoData([null, null])]
    [BitAutoData([null, false])]
    [BitAutoData([null, true])]
    [BitAutoData(false, null)]
    [BitAutoData(true, null)]
    [BitAutoData(false, false)]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(true, true)]
    [NotificationStatusDetailsListCustomize(5)]
    public async Task ListAsync_StatusFilter_ReturnedMatchingNotifications(bool? readStatusFilter, bool? deletedStatusFilter,
        SutProvider<NotificationsController> sutProvider,
        IEnumerable<NotificationStatusDetails> notificationStatusDetailsEnumerable)
    {
        var notificationStatusDetailsList = notificationStatusDetailsEnumerable
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreationDate)
            .ToList();

        sutProvider.GetDependency<IGetNotificationStatusDetailsForUserQuery>()
            .GetByUserIdStatusFilterAsync(Arg.Any<NotificationStatusFilter>(), Arg.Any<PageOptions>())
            .Returns(new PagedResult<NotificationStatusDetails> { Data = notificationStatusDetailsList });

        var expectedNotificationStatusDetailsMap = notificationStatusDetailsList
            .Take(10)
            .ToDictionary(n => n.Id);

        var listResponse = await sutProvider.Sut.ListAsync(new NotificationFilterRequestModel
        {
            ReadStatusFilter = readStatusFilter,
            DeletedStatusFilter = deletedStatusFilter
        });

        Assert.Equal("list", listResponse.Object);
        Assert.Equal(5, listResponse.Data.Count());
        Assert.All(listResponse.Data, notificationResponseModel =>
        {
            Assert.Equal("notification", notificationResponseModel.Object);
            Assert.True(expectedNotificationStatusDetailsMap.ContainsKey(notificationResponseModel.Id));
            var expectedNotificationStatusDetails = expectedNotificationStatusDetailsMap[notificationResponseModel.Id];
            Assert.NotNull(expectedNotificationStatusDetails);
            Assert.Equal(expectedNotificationStatusDetails.Id, notificationResponseModel.Id);
            Assert.Equal(expectedNotificationStatusDetails.Priority, notificationResponseModel.Priority);
            Assert.Equal(expectedNotificationStatusDetails.Title, notificationResponseModel.Title);
            Assert.Equal(expectedNotificationStatusDetails.Body, notificationResponseModel.Body);
            Assert.Equal(expectedNotificationStatusDetails.RevisionDate, notificationResponseModel.Date);
            Assert.Equal(expectedNotificationStatusDetails.ReadDate, notificationResponseModel.ReadDate);
            Assert.Equal(expectedNotificationStatusDetails.DeletedDate, notificationResponseModel.DeletedDate);
        });
        Assert.Null(listResponse.ContinuationToken);

        await sutProvider.GetDependency<IGetNotificationStatusDetailsForUserQuery>()
            .Received(1)
            .GetByUserIdStatusFilterAsync(Arg.Is<NotificationStatusFilter>(filter =>
                    filter.Read == readStatusFilter && filter.Deleted == deletedStatusFilter),
                Arg.Is<PageOptions>(pageOptions =>
                    pageOptions.ContinuationToken == null && pageOptions.PageSize == 10));
    }

    [Theory]
    [BitAutoData]
    [NotificationStatusDetailsListCustomize(19)]
    public async Task ListAsync_PagingRequestNoContinuationToken_ReturnedFirst10MatchingNotifications(
        SutProvider<NotificationsController> sutProvider,
        IEnumerable<NotificationStatusDetails> notificationStatusDetailsEnumerable)
    {
        var notificationStatusDetailsList = notificationStatusDetailsEnumerable
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreationDate)
            .ToList();

        sutProvider.GetDependency<IGetNotificationStatusDetailsForUserQuery>()
            .GetByUserIdStatusFilterAsync(Arg.Any<NotificationStatusFilter>(), Arg.Any<PageOptions>())
            .Returns(new PagedResult<NotificationStatusDetails>
            { Data = notificationStatusDetailsList.Take(10).ToList(), ContinuationToken = "2" });

        var expectedNotificationStatusDetailsMap = notificationStatusDetailsList
            .Take(10)
            .ToDictionary(n => n.Id);

        var listResponse = await sutProvider.Sut.ListAsync(new NotificationFilterRequestModel());

        Assert.Equal("list", listResponse.Object);
        Assert.Equal(10, listResponse.Data.Count());
        Assert.All(listResponse.Data, notificationResponseModel =>
        {
            Assert.Equal("notification", notificationResponseModel.Object);
            Assert.True(expectedNotificationStatusDetailsMap.ContainsKey(notificationResponseModel.Id));
            var expectedNotificationStatusDetails = expectedNotificationStatusDetailsMap[notificationResponseModel.Id];
            Assert.NotNull(expectedNotificationStatusDetails);
            Assert.Equal(expectedNotificationStatusDetails.Id, notificationResponseModel.Id);
            Assert.Equal(expectedNotificationStatusDetails.Priority, notificationResponseModel.Priority);
            Assert.Equal(expectedNotificationStatusDetails.Title, notificationResponseModel.Title);
            Assert.Equal(expectedNotificationStatusDetails.Body, notificationResponseModel.Body);
            Assert.Equal(expectedNotificationStatusDetails.RevisionDate, notificationResponseModel.Date);
            Assert.Equal(expectedNotificationStatusDetails.ReadDate, notificationResponseModel.ReadDate);
            Assert.Equal(expectedNotificationStatusDetails.DeletedDate, notificationResponseModel.DeletedDate);
        });
        Assert.Equal("2", listResponse.ContinuationToken);

        await sutProvider.GetDependency<IGetNotificationStatusDetailsForUserQuery>()
            .Received(1)
            .GetByUserIdStatusFilterAsync(Arg.Any<NotificationStatusFilter>(),
                Arg.Is<PageOptions>(pageOptions =>
                    pageOptions.ContinuationToken == null && pageOptions.PageSize == 10));
    }

    [Theory]
    [BitAutoData]
    [NotificationStatusDetailsListCustomize(19)]
    public async Task ListAsync_PagingRequestUsingContinuationToken_ReturnedLast9MatchingNotifications(
        SutProvider<NotificationsController> sutProvider,
        IEnumerable<NotificationStatusDetails> notificationStatusDetailsEnumerable)
    {
        var notificationStatusDetailsList = notificationStatusDetailsEnumerable
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreationDate)
            .ToList();

        sutProvider.GetDependency<IGetNotificationStatusDetailsForUserQuery>()
            .GetByUserIdStatusFilterAsync(Arg.Any<NotificationStatusFilter>(), Arg.Any<PageOptions>())
            .Returns(new PagedResult<NotificationStatusDetails>
            { Data = notificationStatusDetailsList.Skip(10).ToList() });

        var expectedNotificationStatusDetailsMap = notificationStatusDetailsList
            .Skip(10)
            .ToDictionary(n => n.Id);

        var listResponse = await sutProvider.Sut.ListAsync(new NotificationFilterRequestModel { ContinuationToken = "2" });

        Assert.Equal("list", listResponse.Object);
        Assert.Equal(9, listResponse.Data.Count());
        Assert.All(listResponse.Data, notificationResponseModel =>
        {
            Assert.Equal("notification", notificationResponseModel.Object);
            Assert.True(expectedNotificationStatusDetailsMap.ContainsKey(notificationResponseModel.Id));
            var expectedNotificationStatusDetails = expectedNotificationStatusDetailsMap[notificationResponseModel.Id];
            Assert.NotNull(expectedNotificationStatusDetails);
            Assert.Equal(expectedNotificationStatusDetails.Id, notificationResponseModel.Id);
            Assert.Equal(expectedNotificationStatusDetails.Priority, notificationResponseModel.Priority);
            Assert.Equal(expectedNotificationStatusDetails.Title, notificationResponseModel.Title);
            Assert.Equal(expectedNotificationStatusDetails.Body, notificationResponseModel.Body);
            Assert.Equal(expectedNotificationStatusDetails.RevisionDate, notificationResponseModel.Date);
            Assert.Equal(expectedNotificationStatusDetails.ReadDate, notificationResponseModel.ReadDate);
            Assert.Equal(expectedNotificationStatusDetails.DeletedDate, notificationResponseModel.DeletedDate);
        });
        Assert.Null(listResponse.ContinuationToken);

        await sutProvider.GetDependency<IGetNotificationStatusDetailsForUserQuery>()
            .Received(1)
            .GetByUserIdStatusFilterAsync(Arg.Any<NotificationStatusFilter>(),
                Arg.Is<PageOptions>(pageOptions =>
                    pageOptions.ContinuationToken == "2" && pageOptions.PageSize == 10));
    }

    [Theory]
    [BitAutoData]
    public async Task MarkAsDeletedAsync_NotificationId_MarkedAsDeleted(
        SutProvider<NotificationsController> sutProvider,
        Guid notificationId)
    {
        await sutProvider.Sut.MarkAsDeletedAsync(notificationId);

        await sutProvider.GetDependency<IMarkNotificationDeletedCommand>()
            .Received(1)
            .MarkDeletedAsync(notificationId);
    }

    [Theory]
    [BitAutoData]
    public async Task MarkAsReadAsync_NotificationId_MarkedAsRead(
        SutProvider<NotificationsController> sutProvider,
        Guid notificationId)
    {
        await sutProvider.Sut.MarkAsReadAsync(notificationId);

        await sutProvider.GetDependency<IMarkNotificationReadCommand>()
            .Received(1)
            .MarkReadAsync(notificationId);
    }
}
