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
    [BitAutoData]
    [NotificationCustomize]
    [CurrentContextCustomize]
    public async void PushNotificationAsync_Notification_Sent(
        SutProvider<AzureQueuePushNotificationService> sutProvider, Notification notification, Guid deviceIdentifier,
        ICurrentContext currentContext)
    {
        currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

        await sutProvider.Sut.PushNotificationAsync(notification);

        await sutProvider.GetDependency<QueueClient>().Received(1)
            .SendMessageAsync(Arg.Is<string>(message =>
                MatchMessage(PushType.SyncNotification, message, new SyncNotificationEquals(notification),
                    deviceIdentifier.ToString())));
    }

    private static bool MatchMessage<T>(PushType pushType, string message, IEquatable<T> expectedPayloadEquatable,
        string contextId)
    {
        var pushNotificationData = JsonSerializer.Deserialize<PushNotificationData<T>>(message);
        return pushNotificationData != null &&
               pushNotificationData.Type == pushType &&
               expectedPayloadEquatable.Equals(pushNotificationData.Payload) &&
               pushNotificationData.ContextId == contextId;
    }

    private class SyncNotificationEquals(Notification notification) : IEquatable<NotificationPushNotification>
    {
        public bool Equals(NotificationPushNotification? other)
        {
            return other != null &&
                   other.Id == notification.Id &&
                   other.Priority == notification.Priority &&
                   other.Global == notification.Global &&
                   other.ClientType == notification.ClientType &&
                   other.UserId.HasValue == notification.UserId.HasValue &&
                   other.UserId == notification.UserId &&
                   other.OrganizationId.HasValue == notification.OrganizationId.HasValue &&
                   other.OrganizationId == notification.OrganizationId &&
                   other.Title == notification.Title &&
                   other.Body == notification.Body &&
                   other.CreationDate == notification.CreationDate &&
                   other.RevisionDate == notification.RevisionDate;
        }
    }
}
