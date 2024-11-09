#nullable enable
using System.Net;
using System.Net.Http.Json;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Api;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Repositories;
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
public class RelayPushNotificationServiceTests
{
    [Theory]
    [BitAutoData]
    [GlobalSettingsCustomize]
    public async void Constructor_DefaultGlobalSettings_CorrectHttpRequests(
        SutProvider<RelayPushNotificationService> sutProvider, GlobalSettings globalSettings,
        MockedHttpMessageHandler mockedHttpMessageHandler, Guid installationId)
    {
        ConfigureSut(sutProvider, globalSettings, installationId);

        var tokenResponse = mockedHttpMessageHandler
            .When(request =>
            {
                if (request.Method != HttpMethod.Post ||
                    !request.RequestUri!.Equals(new Uri("http://installation-identity-test.localhost/connect/token")))
                {
                    return false;
                }

                Assert.NotNull(request.Content);
                Assert.NotNull(request.Content.Headers.ContentType);
                Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType.MediaType);

                var formReader = new FormReader(request.Content.ReadAsStream()).ReadForm();

                Assert.Contains("scope", formReader);
                Assert.Equal("api.push", formReader["scope"]);
                Assert.Contains("client_id", formReader);
                Assert.Equal($"installation.{globalSettings.Installation.Id}", formReader["client_id"]);
                Assert.Contains("client_secret", formReader);
                Assert.Equal("installation-key", formReader["client_secret"]);

                return true;
            })
            .RespondWith(HttpStatusCode.OK, new StringContent("{\"access_token\":\"token\"}"));
        var sendResponse = mockedHttpMessageHandler
            .When(request =>
            {
                if (request.Method != HttpMethod.Post ||
                    !request.RequestUri!.Equals(new Uri("http://push-relay-test.localhost/push/send")))
                {
                    return false;
                }

                Assert.Equal("Bearer token", request.Headers.Authorization?.ToString());

                return true;
            })
            .RespondWith(HttpStatusCode.OK);

        await sutProvider.Sut.SendAsync(HttpMethod.Post, "push/send", "payload");

        Assert.Equal(1, tokenResponse.NumberOfResponses);
        Assert.Equal(1, sendResponse.NumberOfResponses);
    }

    [Theory]
    [BitAutoData]
    [NotificationCustomize]
    [CurrentContextCustomize]
    [GlobalSettingsCustomize]
    public async void PushSyncNotificationAsync_GlobalNotification_NothingSent(
        SutProvider<RelayPushNotificationService> sutProvider, Notification notification,
        MockedHttpMessageHandler mockedHttpMessageHandler, GlobalSettings globalSettings, Guid installationId)
    {
        ConfigureSut(sutProvider, globalSettings, installationId);

        var response = mockedHttpMessageHandler
            .When(request => true)
            .RespondWith(HttpStatusCode.OK);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        Assert.Equal(0, response.NumberOfResponses);
    }

    [Theory]
    [BitAutoData(true, true, true)]
    [BitAutoData(true, true, false)]
    [BitAutoData(true, false, true)]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, true)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(false, false, false)]
    [NotificationCustomize(false)]
    [CurrentContextCustomize]
    [GlobalSettingsCustomize]
    public async void PushSyncNotificationAsync_NotificationUserIdSet_SentToUser(bool withOrganizationId,
        bool withDeviceIdentifier, bool deviceFound, Device device,
        SutProvider<RelayPushNotificationService> sutProvider, Notification notification, Guid deviceIdentifier,
        ICurrentContext currentContext, MockedHttpMessageHandler mockedHttpMessageHandler,
        GlobalSettings globalSettings, Guid installationId)
    {
        if (!withOrganizationId)
        {
            notification.OrganizationId = null;
        }

        ConfigureSut(sutProvider, globalSettings, installationId);

        if (withDeviceIdentifier)
        {
            currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
            if (deviceFound)
            {
                sutProvider.GetDependency<IDeviceRepository>().GetByIdentifierAsync(deviceIdentifier.ToString())
                    .Returns(device);
            }
        }

        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

        var tokenResponse = mockedHttpMessageHandler
            .When(request =>
                request.Method == HttpMethod.Post && request.RequestUri!.ToString().EndsWith("/connect/token"))
            .RespondWith(HttpStatusCode.OK, new StringContent("{\"access_token\":\"token\"}"));
        var sendResponse = mockedHttpMessageHandler
            .When(request =>
            {
                if (request.Method != HttpMethod.Post || !request.RequestUri!.ToString().EndsWith("/push/send"))
                {
                    return false;
                }

                Assert.NotNull(request.Content);
                var jsonContent = Assert.IsType<JsonContent>(request.Content);
                Assert.NotNull(jsonContent.Value);

                var pushSendRequest = Assert.IsType<PushSendRequestModel>(jsonContent.Value);
                AssertRequest(pushSendRequest,
                    new PushSendRequestModel
                    {
                        Type = PushType.SyncNotification,
                        Payload = new SyncNotificationPushNotification
                        {
                            Id = notification.Id,
                            UserId = notification.UserId,
                            OrganizationId = notification.OrganizationId,
                            ClientType = notification.ClientType,
                            RevisionDate = notification.RevisionDate
                        },
                        UserId = notification.UserId!.ToString(),
                        OrganizationId = null,
                        DeviceId = withDeviceIdentifier && deviceFound ? device.Id.ToString() : null,
                        Identifier = withDeviceIdentifier ? deviceIdentifier.ToString() : null,
                        ClientType = notification.ClientType
                    },
                    new SyncNotificationEquals());
                return true;
            })
            .RespondWith(HttpStatusCode.OK);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        Assert.Equal(1, tokenResponse.NumberOfResponses);
        Assert.Equal(1, sendResponse.NumberOfResponses);
    }

    [Theory]
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [NotificationCustomize(false)]
    [CurrentContextCustomize]
    [GlobalSettingsCustomize]
    public async void PushSyncNotificationAsync_NotificationOrganizationIdSet_SentToOrganization(
        bool withDeviceIdentifier, bool deviceFound, Device device,
        SutProvider<RelayPushNotificationService> sutProvider, Notification notification, Guid deviceIdentifier,
        ICurrentContext currentContext, MockedHttpMessageHandler mockedHttpMessageHandler,
        GlobalSettings globalSettings, Guid installationId)
    {
        notification.UserId = null;

        ConfigureSut(sutProvider, globalSettings, installationId);

        if (withDeviceIdentifier)
        {
            currentContext.DeviceIdentifier.Returns(deviceIdentifier.ToString());
            if (deviceFound)
            {
                sutProvider.GetDependency<IDeviceRepository>().GetByIdentifierAsync(deviceIdentifier.ToString())
                    .Returns(device);
            }
        }

        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext!.RequestServices
            .GetService(Arg.Any<Type>()).Returns(currentContext);

        var tokenResponse = mockedHttpMessageHandler
            .When(request =>
                request.Method == HttpMethod.Post && request.RequestUri!.ToString().EndsWith("/connect/token"))
            .RespondWith(HttpStatusCode.OK, new StringContent("{\"access_token\":\"token\"}"));
        var sendResponse = mockedHttpMessageHandler
            .When(request =>
            {
                if (request.Method != HttpMethod.Post || !request.RequestUri!.ToString().EndsWith("/push/send"))
                {
                    return false;
                }

                Assert.NotNull(request.Content);
                var jsonContent = Assert.IsType<JsonContent>(request.Content);
                Assert.NotNull(jsonContent.Value);

                var pushSendRequest = Assert.IsType<PushSendRequestModel>(jsonContent.Value);
                AssertRequest(pushSendRequest,
                    new PushSendRequestModel
                    {
                        Type = PushType.SyncNotification,
                        Payload = new SyncNotificationPushNotification
                        {
                            Id = notification.Id,
                            UserId = notification.UserId,
                            OrganizationId = notification.OrganizationId,
                            ClientType = notification.ClientType,
                            RevisionDate = notification.RevisionDate
                        },
                        UserId = null,
                        OrganizationId = notification.OrganizationId!.ToString(),
                        DeviceId = withDeviceIdentifier && deviceFound ? device.Id.ToString() : null,
                        Identifier = withDeviceIdentifier ? deviceIdentifier.ToString() : null,
                        ClientType = notification.ClientType
                    },
                    new SyncNotificationEquals());
                return true;
            })
            .RespondWith(HttpStatusCode.OK);

        await sutProvider.Sut.PushSyncNotificationAsync(notification);

        Assert.Equal(1, tokenResponse.NumberOfResponses);
        Assert.Equal(1, sendResponse.NumberOfResponses);
    }

    private static void AssertRequest<T>(PushSendRequestModel request, PushSendRequestModel expectedRequest,
        IEqualityComparer<T> expectedPayloadEquatable)
    {
        Assert.Equal(expectedRequest.Type, request.Type);
        Assert.Equal(expectedRequest.UserId, request.UserId);
        Assert.Equal(expectedRequest.OrganizationId, request.OrganizationId);
        Assert.Equal(expectedRequest.DeviceId, request.DeviceId);
        Assert.Equal(expectedRequest.Identifier, request.Identifier);
        Assert.Equal(expectedRequest.ClientType, request.ClientType);
        Assert.Equal((T)expectedRequest.Payload, (T)request.Payload, expectedPayloadEquatable);
    }

    private class SyncNotificationEquals : IEqualityComparer<SyncNotificationPushNotification>
    {
        public bool Equals(SyncNotificationPushNotification? x, SyncNotificationPushNotification? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Id.Equals(y.Id) && Nullable.Equals(x.UserId, y.UserId) &&
                   Nullable.Equals(x.OrganizationId, y.OrganizationId) && x.ClientType == y.ClientType &&
                   x.RevisionDate.Equals(y.RevisionDate);
        }

        public int GetHashCode(SyncNotificationPushNotification obj)
        {
            return HashCode.Combine(obj.Id, obj.UserId, obj.OrganizationId, (int)obj.ClientType, obj.RevisionDate);
        }
    }

    private void ConfigureSut(SutProvider<RelayPushNotificationService> sutProvider, GlobalSettings globalSettings,
        Guid installationId)
    {
        globalSettings.PushRelayBaseUri = "http://push-relay-test.localhost";
        globalSettings.Installation.IdentityUri = "http://installation-identity-test.localhost";
        globalSettings.Installation.Id = installationId;
        globalSettings.Installation.Key = "installation-key";
        sutProvider.SetDependency(typeof(GlobalSettings), globalSettings)
            .Create();
    }
}
