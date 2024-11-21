#nullable enable
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Services;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class MultiServicePushNotificationServiceTests
{
    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize]
    [NotificationStatusCustomize]
    public async Task PushSyncNotificationCreateAsync_Notification_Sent(bool notificationStatusNull,
        SutProvider<MultiServicePushNotificationService> sutProvider, Notification notification,
        NotificationStatus notificationStatus)
    {
        await sutProvider.Sut.PushSyncNotificationCreateAsync(notification,
            notificationStatusNull ? null : notificationStatus);

        var expectedNotificationStatus = notificationStatusNull ? null : notificationStatus;
        await sutProvider.GetDependency<IEnumerable<IPushNotificationService>>()
            .First()
            .Received(1)
            .PushSyncNotificationCreateAsync(notification, expectedNotificationStatus);
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    [NotificationCustomize]
    [NotificationStatusCustomize]
    public async Task PushSyncNotificationUpdateAsync_Notification_Sent(bool notificationStatusNull,
        SutProvider<MultiServicePushNotificationService> sutProvider, Notification notification,
        NotificationStatus notificationStatus)
    {
        await sutProvider.Sut.PushSyncNotificationUpdateAsync(notification,
            notificationStatusNull ? null : notificationStatus);

        var expectedNotificationStatus = notificationStatusNull ? null : notificationStatus;
        await sutProvider.GetDependency<IEnumerable<IPushNotificationService>>()
            .First()
            .Received(1)
            .PushSyncNotificationUpdateAsync(notification, expectedNotificationStatus);
    }

    [Theory]
    [BitAutoData([null, null])]
    [BitAutoData(ClientType.All, null)]
    [BitAutoData([null, "test device id"])]
    [BitAutoData(ClientType.All, "test device id")]
    public async Task SendPayloadToUserAsync_Message_Sent(ClientType? clientType, string? deviceId, string userId,
        PushType type, object payload, string identifier, SutProvider<MultiServicePushNotificationService> sutProvider)
    {
        await sutProvider.Sut.SendPayloadToUserAsync(userId, type, payload, identifier, deviceId, clientType);

        await sutProvider.GetDependency<IEnumerable<IPushNotificationService>>()
            .First()
            .Received(1)
            .SendPayloadToUserAsync(userId, type, payload, identifier, deviceId, clientType);
    }

    [Theory]
    [BitAutoData([null, null])]
    [BitAutoData(ClientType.All, null)]
    [BitAutoData([null, "test device id"])]
    [BitAutoData(ClientType.All, "test device id")]
    public async Task SendPayloadToOrganizationAsync_Message_Sent(ClientType? clientType, string? deviceId,
        string organizationId, PushType type, object payload, string identifier,
        SutProvider<MultiServicePushNotificationService> sutProvider)
    {
        await sutProvider.Sut.SendPayloadToOrganizationAsync(organizationId, type, payload, identifier, deviceId,
            clientType);

        await sutProvider.GetDependency<IEnumerable<IPushNotificationService>>()
            .First()
            .Received(1)
            .SendPayloadToOrganizationAsync(organizationId, type, payload, identifier, deviceId, clientType);
    }
}
