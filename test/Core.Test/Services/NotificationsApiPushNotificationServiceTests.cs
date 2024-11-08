#nullable enable
using System.Net;
using System.Net.Http.Json;
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
using Bit.Test.Common.MockedHttpClient;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
[HttpClientCustomize]
public class NotificationsApiPushNotificationServiceTests
{
    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    [CurrentContextCustomize]
    public async void PushSyncNotificationAsync_Notification_Sent(
        SutProvider<NotificationsApiPushNotificationService> sutProvider, Notification notification,
        Guid deviceIdentifier, ICurrentContext currentContext, MockedHttpMessageHandler mockedHttpMessageHandler)
    {
        var tokenResponse = mockedHttpMessageHandler
            .When(request =>
                request.Method == HttpMethod.Post && request.RequestUri!.ToString().EndsWith("/connect/token"))
            .RespondWith(HttpStatusCode.OK, new StringContent("{\"access_token\":\"token\"}"));
        var sendResponse = mockedHttpMessageHandler
            .When(request =>
            {
                if (request.Method != HttpMethod.Post || !request.RequestUri!.ToString().EndsWith("/send") ||
                    request.Content is not JsonContent jsonContent || jsonContent.Value == null)
                {
                    return false;
                }

                var pushNotificationData = (PushNotificationData<SyncNotificationPushNotification>)jsonContent.Value;
                return MatchMessage(PushType.SyncNotification, pushNotificationData,
                    new SyncNotificationEquals(notification), deviceIdentifier.ToString());
            })
            .RespondWith(HttpStatusCode.OK);

        currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        Assert.Equal(1, tokenResponse.NumberOfResponses);
        Assert.Equal(1, sendResponse.NumberOfResponses);
    }

    private static bool MatchMessage<T>(PushType pushType, PushNotificationData<T> pushNotificationData,
        IEquatable<T> expectedPayloadEquatable, string contextId)
    {
        return pushNotificationData.Type == pushType &&
               expectedPayloadEquatable.Equals(pushNotificationData.Payload) &&
               pushNotificationData.ContextId == contextId;
    }

    private class SyncNotificationEquals(Notification notification) : IEquatable<SyncNotificationPushNotification>
    {
        public bool Equals(SyncNotificationPushNotification? other)
        {
            return other != null &&
                   other.Id == notification.Id &&
                   other.UserId == notification.UserId &&
                   other.OrganizationId == notification.OrganizationId &&
                   other.ClientType == notification.ClientType &&
                   other.RevisionDate == notification.RevisionDate;
        }
    }
}
