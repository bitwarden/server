#nullable enable
using System.Net;
using System.Net.Http.Json;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.CurrentContextFixtures;
using Bit.Core.Test.NotificationCenter.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize(false)]
[HttpClientCustomize]
public class NotificationsApiPushNotificationServiceTests
{
    [Theory]
    [BitAutoData]
    [GlobalSettingsCustomize]
    public async void Constructor_DefaultGlobalSettings_CorrectHttpRequests(
        SutProvider<NotificationsApiPushNotificationService> sutProvider, GlobalSettings globalSettings,
        MockedHttpMessageHandler mockedHttpMessageHandler)
    {
        globalSettings.SelfHosted = true;
        globalSettings.ProjectName = "Notifications";
        globalSettings.InternalIdentityKey = "internal-identity-key";
        sutProvider.SetDependency(typeof(GlobalSettings), globalSettings)
            .Create();

        var tokenResponse = mockedHttpMessageHandler
            .When(request =>
            {
                if (request.Method != HttpMethod.Post ||
                    !request.RequestUri!.Equals(new Uri("http://identity:5000/connect/token")) ||
                    request.Content == null ||
                    request.Content.Headers.ContentType?.MediaType != "application/x-www-form-urlencoded")
                {
                    return false;
                }

                var formReader = new FormReader(request.Content.ReadAsStream()).ReadForm();

                return formReader["scope"] == "internal" && formReader["client_id"] == "internal.Notifications" &&
                       formReader["client_secret"] == "internal-identity-key";
            })
            .RespondWith(HttpStatusCode.OK, new StringContent("{\"access_token\":\"token\"}"));
        var sendResponse = mockedHttpMessageHandler
            .When(request => request.Method == HttpMethod.Post &&
                             (request.RequestUri?.Equals(new Uri("http://notifications:5000/send")) ?? false) &&
                             request.Headers.Authorization?.ToString() == "Bearer token")
            .RespondWith(HttpStatusCode.OK);

        await sutProvider.Sut.SendAsync(HttpMethod.Post, "send", "payload");

        Assert.Equal(1, tokenResponse.NumberOfResponses);
        Assert.Equal(1, sendResponse.NumberOfResponses);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    [CurrentContextCustomize]
    public async void PushSyncNotificationAsync_Notification_Sent(
        SutProvider<NotificationsApiPushNotificationService> sutProvider, Notification notification,
        Guid deviceIdentifier, ICurrentContext currentContext, MockedHttpMessageHandler mockedHttpMessageHandler)
    {
        sutProvider.Create();
        currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

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
