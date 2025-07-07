using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Queues;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.NotificationHub;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;
using static Bit.Core.Settings.GlobalSettings;

namespace Bit.Api.IntegrationTest.Platform.Controllers;

public class PushControllerTests
{
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly Guid _deviceId = Guid.NewGuid();

    public static IEnumerable<object[]> SendData()
    {
        static object[] Typed<T>(PushSendRequestModel<T> pushSendRequestModel, string expectedHubTagExpression, bool expectHubCall = true)
        {
            return [pushSendRequestModel, expectedHubTagExpression, expectHubCall];
        }

        static object[] UserTyped(PushType pushType)
        {
            return Typed(new PushSendRequestModel<UserPushNotification>
            {
                Type = pushType,
                UserId = _userId,
                DeviceId = _deviceId,
                Payload = new UserPushNotification
                {
                    Date = DateTime.UtcNow,
                    UserId = _userId,
                },
            }, $"(template:payload_userId:%installation%_{_userId})");
        }

        // User cipher
        yield return Typed(new PushSendRequestModel<SyncCipherPushNotification>
        {
            Type = PushType.SyncCipherUpdate,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new SyncCipherPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        // Organization cipher, an org cipher would not naturally be synced from our 
        // code but it is technically possible to be submitted to the endpoint.
        yield return Typed(new PushSendRequestModel<SyncCipherPushNotification>
        {
            Type = PushType.SyncCipherUpdate,
            OrganizationId = _organizationId,
            DeviceId = _deviceId,
            Payload = new SyncCipherPushNotification
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
            },
        }, $"(template:payload && organizationId:%installation%_{_organizationId})");

        yield return Typed(new PushSendRequestModel<SyncCipherPushNotification>
        {
            Type = PushType.SyncCipherCreate,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new SyncCipherPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        // Organization cipher, an org cipher would not naturally be synced from our 
        // code but it is technically possible to be submitted to the endpoint.
        yield return Typed(new PushSendRequestModel<SyncCipherPushNotification>
        {
            Type = PushType.SyncCipherCreate,
            OrganizationId = _organizationId,
            DeviceId = _deviceId,
            Payload = new SyncCipherPushNotification
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
            },
        }, $"(template:payload && organizationId:%installation%_{_organizationId})");

        yield return Typed(new PushSendRequestModel<SyncCipherPushNotification>
        {
            Type = PushType.SyncCipherDelete,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new SyncCipherPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        // Organization cipher, an org cipher would not naturally be synced from our 
        // code but it is technically possible to be submitted to the endpoint.
        yield return Typed(new PushSendRequestModel<SyncCipherPushNotification>
        {
            Type = PushType.SyncCipherDelete,
            OrganizationId = _organizationId,
            DeviceId = _deviceId,
            Payload = new SyncCipherPushNotification
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
            },
        }, $"(template:payload && organizationId:%installation%_{_organizationId})");

        yield return Typed(new PushSendRequestModel<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderDelete,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new SyncFolderPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return Typed(new PushSendRequestModel<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderCreate,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new SyncFolderPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return Typed(new PushSendRequestModel<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderCreate,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new SyncFolderPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return UserTyped(PushType.SyncCiphers);
        yield return UserTyped(PushType.SyncVault);
        yield return UserTyped(PushType.SyncOrganizations);
        yield return UserTyped(PushType.SyncOrgKeys);
        yield return UserTyped(PushType.SyncSettings);
        yield return UserTyped(PushType.LogOut);
        yield return UserTyped(PushType.RefreshSecurityTasks);

        yield return Typed(new PushSendRequestModel<AuthRequestPushNotification>
        {
            Type = PushType.AuthRequest,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new AuthRequestPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return Typed(new PushSendRequestModel<AuthRequestPushNotification>
        {
            Type = PushType.AuthRequestResponse,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new AuthRequestPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return Typed(new PushSendRequestModel<NotificationPushNotification>
        {
            Type = PushType.Notification,
            UserId = _userId,
            DeviceId = _deviceId,
            Payload = new NotificationPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return Typed(new PushSendRequestModel<NotificationPushNotification>
        {
            Type = PushType.Notification,
            UserId = _userId,
            DeviceId = _deviceId,
            ClientType = ClientType.All,
            Payload = new NotificationPushNotification
            {
                Id = Guid.NewGuid(),
                Global = true,
            },
        }, $"(template:payload_userId:%installation%_{_userId})");

        yield return Typed(new PushSendRequestModel<NotificationPushNotification>
        {
            Type = PushType.NotificationStatus,
            OrganizationId = _organizationId,
            DeviceId = _deviceId,
            Payload = new NotificationPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload && organizationId:%installation%_{_organizationId})");

        yield return Typed(new PushSendRequestModel<NotificationPushNotification>
        {
            Type = PushType.NotificationStatus,
            OrganizationId = _organizationId,
            DeviceId = _deviceId,
            Payload = new NotificationPushNotification
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
            },
        }, $"(template:payload && organizationId:%installation%_{_organizationId})");
    }

    [Theory]
    [MemberData(nameof(SendData))]
    public async Task Send_Works<T>(PushSendRequestModel<T> pushSendRequestModel, string expectedHubTagExpression, bool expectHubCall)
    {
        var (apiFactory, httpClient, installation, queueClient, notificationHubProxy) = await SetupTest();

        // Act
        var pushSendResponse = await httpClient.PostAsJsonAsync("push/send", pushSendRequestModel);

        // Assert 
        pushSendResponse.EnsureSuccessStatusCode();

        // Relayed notifications, the ones coming to this endpoint should
        // not make their way into our Azure Queue and instead should only be sent to Azure Notifications
        // hub.
        await queueClient
            .Received(0)
            .SendMessageAsync(Arg.Any<string>());

        // Check that this notification was sent through hubs the expected number of times
        await notificationHubProxy
            .Received(expectHubCall ? 1 : 0)
            .SendTemplateNotificationAsync(
                Arg.Any<Dictionary<string, string>>(),
                Arg.Is(expectedHubTagExpression.Replace("%installation%", installation.Id.ToString()))
            );

        // TODO: Expect on the dictionary more?

        // Notifications being relayed from SH should have the device id
        // tracked so that we can later send the notification to that device.
        await apiFactory.GetService<IInstallationDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<InstallationDeviceEntity>(
                ide => ide.PartitionKey == installation.Id.ToString() && ide.RowKey == pushSendRequestModel.DeviceId.ToString()
            ));
    }

    [Fact]
    public async Task Send_InstallationNotification_NotAuthenticatedInstallation_Fails()
    {
        var (_, httpClient, _, _, _) = await SetupTest();

        var response = await httpClient.PostAsJsonAsync("push/send", new PushSendRequestModel<object>
        {
            Type = PushType.NotificationStatus,
            InstallationId = Guid.NewGuid(),
            Payload = new { }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal(JsonValueKind.Object, body.GetValueKind());
        Assert.True(body.AsObject().TryGetPropertyValue("message", out var message));
        Assert.Equal(JsonValueKind.String, message.GetValueKind());
        Assert.Equal("InstallationId does not match current context.", message.GetValue<string>());
    }

    [Fact]
    public async Task Send_InstallationNotification_Works()
    {
        var (apiFactory, httpClient, installation, _, notificationHubProxy) = await SetupTest();

        var deviceId = Guid.NewGuid();

        var response = await httpClient.PostAsJsonAsync("push/send", new PushSendRequestModel<object>
        {
            Type = PushType.NotificationStatus,
            InstallationId = installation.Id,
            Payload = new { },
            DeviceId = deviceId,
            ClientType = ClientType.Web,
        });

        response.EnsureSuccessStatusCode();

        await notificationHubProxy
            .Received(1)
            .SendTemplateNotificationAsync(
                Arg.Any<Dictionary<string, string>>(),
                Arg.Is($"(template:payload && installationId:{installation.Id} && clientType:Web)")
            );

        await apiFactory.GetService<IInstallationDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<InstallationDeviceEntity>(
                ide => ide.PartitionKey == installation.Id.ToString() && ide.RowKey == deviceId.ToString()
            ));
    }

    [Fact]
    public async Task Send_NoOrganizationNoInstallationNoUser_FailsModelValidation()
    {
        var (_, client, _, _, _) = await SetupTest();

        var response = await client.PostAsJsonAsync("push/send", new PushSendRequestModel<object>
        {
            Type = PushType.AuthRequest,
            Payload = new { },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal(JsonValueKind.Object, body.GetValueKind());
        Assert.True(body.AsObject().TryGetPropertyValue("message", out var message));
        Assert.Equal(JsonValueKind.String, message.GetValueKind());
        Assert.Equal("The model state is invalid.", message.GetValue<string>());
    }

    private static async Task<(ApiApplicationFactory Factory, HttpClient AuthedClient, Installation Installation, QueueClient MockedQueue, INotificationHubProxy MockedHub)> SetupTest()
    {
        // Arrange
        var apiFactory = new ApiApplicationFactory();

        var queueClient = Substitute.For<QueueClient>();

        // Substitute the underlying queue messages will go to.
        apiFactory.ConfigureServices(services =>
        {
            var queueClientService = services.FirstOrDefault(
                sd => sd.ServiceKey == (object)"notifications"
                    && sd.ServiceType == typeof(QueueClient)
            ) ?? throw new InvalidOperationException("Expected service was not found.");

            services.Remove(queueClientService);

            services.AddKeyedSingleton("notifications", queueClient);
        });

        var notificationHubProxy = Substitute.For<INotificationHubProxy>();

        apiFactory.SubstituteService<INotificationHubPool>(s =>
        {
            s.AllClients
                .Returns(notificationHubProxy);
        });

        apiFactory.SubstituteService<IInstallationDeviceRepository>(s => { });

        // Setup as cloud with NotificationHub setup and Azure Queue
        apiFactory.UpdateConfiguration("GlobalSettings:Notifications:ConnectionString", "any_value");

        // Configure hubs
        var index = 0;
        void AddHub(NotificationHubSettings notificationHubSettings)
        {
            apiFactory.UpdateConfiguration(
                $"GlobalSettings:NotificationHubPool:NotificationHubs:{index}:ConnectionString",
                notificationHubSettings.ConnectionString
            );
            apiFactory.UpdateConfiguration(
                $"GlobalSettings:NotificationHubPool:NotificationHubs:{index}:HubName",
                notificationHubSettings.HubName
            );
            apiFactory.UpdateConfiguration(
                $"GlobalSettings:NotificationHubPool:NotificationHubs:{index}:RegistrationStartDate",
                notificationHubSettings.RegistrationStartDate?.ToString()
            );
            apiFactory.UpdateConfiguration(
                $"GlobalSettings:NotificationHubPool:NotificationHubs:{index}:RegistrationEndDate",
                notificationHubSettings.RegistrationEndDate?.ToString()
            );
            index++;
        }

        AddHub(new NotificationHubSettings
        {
            ConnectionString = "some_value",
            RegistrationStartDate = DateTime.UtcNow.AddDays(-2),
        });

        var httpClient = apiFactory.CreateClient();

        // Add installation into database
        var installationRepository = apiFactory.GetService<IInstallationRepository>();
        var installation = await installationRepository.CreateAsync(new Installation
        {
            Key = "my_test_key",
            Email = "test@example.com",
            Enabled = true,
        });

        var identityClient = apiFactory.Identity.CreateDefaultClient();

        var connectTokenResponse = await identityClient.PostAsync("connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "scope", "api.push" },
            { "client_id", $"installation.{installation.Id}" },
            { "client_secret", installation.Key },
        }));

        connectTokenResponse.EnsureSuccessStatusCode();

        var connectTokenResponseModel = await connectTokenResponse.Content.ReadFromJsonAsync<JsonNode>();

        // Setup authentication
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            connectTokenResponseModel["token_type"].GetValue<string>(),
            connectTokenResponseModel["access_token"].GetValue<string>()
        );

        return (apiFactory, httpClient, installation, queueClient, notificationHubProxy);
    }
}
