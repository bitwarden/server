﻿#nullable enable
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Platform.Push.Internal.Test;

[SutProviderCustomize]
public class MultiServicePushNotificationServiceTests
{
    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    public async Task PushSyncNotificationAsync_Notification_Sent(
        SutProvider<MultiServicePushNotificationService> sutProvider, Notification notification)
    {
        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        await sutProvider.GetDependency<IEnumerable<IPushNotificationService>>()
            .First()
            .Received(1)
            .PushSyncNotificationAsync(notification);
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
