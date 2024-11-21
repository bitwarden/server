#nullable enable
using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.CurrentContextFixtures;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[QueueClientCustomize]
[SutProviderCustomize]
public class AzureQueuePushNotificationServiceTests
{
    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize]
    [NotificationStatusCustomize]
    [CurrentContextCustomize]
    public async Task PushSyncNotificationCreateAsync_Notification_Sent(bool notificationStatusNull,
        SutProvider<AzureQueuePushNotificationService> sutProvider, Notification notification, Guid deviceIdentifier,
        ICurrentContext currentContext, NotificationStatus notificationStatus)
    {
        var expectedNotificationStatus = notificationStatusNull ? null : notificationStatus;
        currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

        await sutProvider.Sut.PushSyncNotificationCreateAsync(notification, expectedNotificationStatus);

        await sutProvider.GetDependency<QueueClient>().Received(1)
            .SendMessageAsync(Arg.Is<string>(message =>
                MatchMessage(PushType.SyncNotificationCreate, message,
                    new SyncNotificationEquals(notification, expectedNotificationStatus),
                    deviceIdentifier.ToString())));
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize]
    [NotificationStatusCustomize]
    [CurrentContextCustomize]
    public async Task PushSyncNotificationUpdateAsync_Notification_Sent(bool notificationStatusNull,
        SutProvider<AzureQueuePushNotificationService> sutProvider, Notification notification, Guid deviceIdentifier,
        ICurrentContext currentContext, NotificationStatus notificationStatus)
    {
        var expectedNotificationStatus = notificationStatusNull ? null : notificationStatus;
        currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

        await sutProvider.Sut.PushSyncNotificationUpdateAsync(notification, expectedNotificationStatus);

        await sutProvider.GetDependency<QueueClient>().Received(1)
            .SendMessageAsync(Arg.Is<string>(message =>
                MatchMessage(PushType.SyncNotificationUpdate, message,
                    new SyncNotificationEquals(notification, expectedNotificationStatus),
                    deviceIdentifier.ToString())));
    }

    private static bool MatchMessage<T>(PushType pushType, string message, IEquatable<T> expectedPayloadEquatable,
        string contextId)
    {
        var pushNotificationData =
            JsonSerializer.Deserialize<PushNotificationData<T>>(message);
        return pushNotificationData != null &&
               pushNotificationData.Type == pushType &&
               expectedPayloadEquatable.Equals(pushNotificationData.Payload) &&
               pushNotificationData.ContextId == contextId;
    }

    private class SyncNotificationEquals(Notification notification, NotificationStatus? notificationStatus)
        : IEquatable<SyncNotificationPushNotification>
    {
        public bool Equals(SyncNotificationPushNotification? other)
        {
            return other != null &&
                   other.Id == notification.Id &&
                   other.UserId == notification.UserId &&
                   other.OrganizationId == notification.OrganizationId &&
                   other.ClientType == notification.ClientType &&
                   other.RevisionDate == notification.RevisionDate &&
                   other.ReadDate == notificationStatus?.ReadDate &&
                   other.DeletedDate == notificationStatus?.DeletedDate;
        }
    }
}
