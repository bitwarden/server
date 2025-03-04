using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Core.Test.Platform.Push.Services;

public class RelayPushNotificationServiceTests
{
    private static readonly Guid _deviceId = Guid.Parse("c4730f80-caaa-4772-97bd-5c0d23a2baa3");
    private static readonly string _deviceIdentifier = "test_device_identifier";

    private readonly RelayPushNotificationService _sut;

    private readonly MockHttpMessageHandler _mockPushClient = new();
    private readonly MockHttpMessageHandler _mockIdentityClient = new();

    private readonly IHttpClientFactory _httpFactory;
    private readonly IDeviceRepository _deviceRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RelayPushNotificationService> _logger;
    private readonly FakeTimeProvider _fakeTimeProvider;

    public RelayPushNotificationServiceTests()
    {
        _httpFactory = Substitute.For<IHttpClientFactory>();

        // Mock HttpClient
        _httpFactory.CreateClient("client")
            .Returns(new HttpClient(_mockPushClient));

        _httpFactory.CreateClient("identity")
            .Returns(new HttpClient(_mockIdentityClient));

        _deviceRepository = Substitute.For<IDeviceRepository>();
        _globalSettings = new GlobalSettings();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _logger = Substitute.For<ILogger<RelayPushNotificationService>>();

        _globalSettings.PushRelayBaseUri = "https://localhost:7777";
        _globalSettings.Installation.Id = Guid.Parse("478c608a-99fd-452a-94f0-af271654e6ee");
        _globalSettings.Installation.IdentityUri = "https://localhost:8888";

        _fakeTimeProvider = new FakeTimeProvider();

        _fakeTimeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        _sut = new RelayPushNotificationService(
            _httpFactory,
            _deviceRepository,
            _globalSettings,
            _httpContextAccessor,
            _logger,
            _fakeTimeProvider
        );
    }

    [Fact]
    public async Task PushSyncCipherCreateAsync_SendsExpectedResponse()
    {
        var collectionId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrganizationId = null,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 1,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                // Currently CollectionIds are not passed along from the method signature
                // to the request body. 
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherCreateAsync(cipher, [collectionId]),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncCipherUpdateAsync_SendsExpectedResponse()
    {
        var collectionId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrganizationId = null,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 0,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                // Currently CollectionIds are not passed along from the method signature
                // to the request body. 
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherUpdateAsync(cipher, [collectionId]),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncCipherDeleteAsync_SendsExpectedResponse()
    {
        var collectionId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrganizationId = null,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = cipher.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 2,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = null,
                ["CollectionIds"] = null,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherDeleteAsync(cipher),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncFolderCreateAsync_SendsExpectedResponse()
    {
        var collectionId = Guid.NewGuid();

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = folder.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 7,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderCreateAsync(folder),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncFolderUpdateAsync_SendsExpectedResponse()
    {
        var collectionId = Guid.NewGuid();

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = folder.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 8,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderUpdateAsync(folder),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncFolderDeleteAsync_SendsExpectedResponse()
    {
        var collectionId = Guid.NewGuid();

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = folder.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 3,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderDeleteAsync(folder),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncCiphersAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 4,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCiphersAsync(userId),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncVaultAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 5,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncVaultAsync(userId),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncOrganizationsAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 17,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationsAsync(userId),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncOrgKeysAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 6,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrgKeysAsync(userId),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncSettingsAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 10,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSettingsAsync(userId),
            expectedPayload
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PushLogOutAsync_SendsExpectedResponse(bool excludeCurrentContext)
    {
        var userId = Guid.NewGuid();

        JsonNode identifier = excludeCurrentContext ? _deviceIdentifier : null;

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = identifier,
            ["Type"] = 11,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushLogOutAsync(userId, excludeCurrentContext),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncSendCreateAsync_SendsExpectedResponse()
    {
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = send.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 12,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendCreateAsync(send),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncSendUpdateAsync_SendsExpectedResponse()
    {
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = send.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 13,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendUpdateAsync(send),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncSendDeleteAsync_SendsExpectedResponse()
    {
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = send.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 14,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendDeleteAsync(send),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushAuthRequestAsync_SendsExpectedResponse()
    {
        var authRequest = new AuthRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = authRequest.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 15,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushAuthRequestAsync(authRequest),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushAuthRequestResponseAsync_SendsExpectedResponse()
    {
        var authRequest = new AuthRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = authRequest.UserId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 16,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushAuthRequestResponseAsync(authRequest),
            expectedPayload
        );
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, "e8e08ce8-8a26-4a65-913a-ba1d8c478b2f", null)]
    [InlineData(false, null, "2f53ee32-edf9-4169-b276-760fe92e03bf")]
    public async Task PushNotificationAsync_SendsExpectedResponse(bool global, string? userId, string? organizationId)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Priority = Priority.High,
            Global = global,
            ClientType = ClientType.All,
            UserId = userId != null ? Guid.Parse(userId) : null,
            OrganizationId = organizationId != null ? Guid.Parse(organizationId) : null,
            Title = "My Title",
            Body = "My Body",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            RevisionDate = DateTime.UtcNow,
        };

        JsonNode installationId = global ? _globalSettings.Installation.Id : null;

        var expectedPayload = new JsonObject
        {
            ["UserId"] = notification.UserId,
            ["OrganizationId"] = notification.OrganizationId,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 20,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = global,
                ["ClientType"] = 0,
                ["UserId"] = notification.UserId,
                ["OrganizationId"] = notification.OrganizationId,
                ["InstallationId"] = installationId,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
                ["ReadDate"] = null,
                ["DeletedDate"] = null,
            },
            ["ClientType"] = 0,
            ["InstallationId"] = installationId?.DeepClone(),
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationAsync(notification),
            expectedPayload
        );
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, "e8e08ce8-8a26-4a65-913a-ba1d8c478b2f", null)]
    [InlineData(false, null, "2f53ee32-edf9-4169-b276-760fe92e03bf")]
    public async Task PushNotificationStatusAsync_SendsExpectedResponse(bool global, string? userId, string? organizationId)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Priority = Priority.High,
            Global = global,
            ClientType = ClientType.All,
            UserId = userId != null ? Guid.Parse(userId) : null,
            OrganizationId = organizationId != null ? Guid.Parse(organizationId) : null,
            Title = "My Title",
            Body = "My Body",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            RevisionDate = DateTime.UtcNow,
        };

        var notificationStatus = new NotificationStatus
        {
            ReadDate = DateTime.UtcNow,
            DeletedDate = DateTime.UtcNow,
        };

        JsonNode installationId = global ? _globalSettings.Installation.Id : null;

        var expectedPayload = new JsonObject
        {
            ["UserId"] = notification.UserId,
            ["OrganizationId"] = notification.OrganizationId,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = _deviceIdentifier,
            ["Type"] = 21,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = global,
                ["ClientType"] = 0,
                ["UserId"] = notification.UserId,
                ["OrganizationId"] = notification.OrganizationId,
                ["InstallationId"] = installationId,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
                ["ReadDate"] = notificationStatus.ReadDate,
                ["DeletedDate"] = notificationStatus.DeletedDate,
            },
            ["ClientType"] = 0,
            ["InstallationId"] = installationId?.DeepClone(),
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationStatusAsync(notification, notificationStatus),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncOrganizationStatusAsync_SendsExpectedResponse()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Enabled = true,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = null,
            ["OrganizationId"] = organization.Id,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 18,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["Enabled"] = organization.Enabled,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationStatusAsync(organization),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushSyncOrganizationCollectionManagementSettingsAsync_SendsExpectedResponse()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            LimitCollectionCreation = true,
            LimitCollectionDeletion = true,
            LimitItemDeletion = true,
        };

        var expectedPayload = new JsonObject
        {
            ["UserId"] = null,
            ["OrganizationId"] = organization.Id,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 19,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["LimitCollectionCreation"] = organization.LimitCollectionCreation,
                ["LimitCollectionDeletion"] = organization.LimitCollectionDeletion,
                ["LimitItemDeletion"] = organization.LimitItemDeletion,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationCollectionManagementSettingsAsync(organization),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushPendingSecurityTasksAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["UserId"] = userId,
            ["OrganizationId"] = null,
            ["DeviceId"] = _deviceId,
            ["Identifier"] = null,
            ["Type"] = 22,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
            ["ClientType"] = null,
            ["InstallationId"] = null,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushPendingSecurityTasksAsync(userId),
            expectedPayload
        );
    }

    [Fact]
    public async Task SendPayloadToInstallationAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.SendPayloadToInstallationAsync("installation_id", PushType.AuthRequest, new {}, null)
        );
    }

    [Fact]
    public async Task SendPayloadToUserAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.SendPayloadToUserAsync("user_id", PushType.AuthRequest, new {}, null)
        );
    }

    [Fact]
    public async Task SendPayloadToOrganizationAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.SendPayloadToOrganizationAsync("organization_id", PushType.AuthRequest, new {}, null)
        );
    }

    private async Task VerifyNotificationAsync(Func<RelayPushNotificationService, Task> test, JsonNode expectedRequestBody)
    {
        var httpContext = new DefaultHttpContext();

        var serviceCollection = new ServiceCollection();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = _deviceIdentifier;
        serviceCollection.AddSingleton(currentContext);

        httpContext.RequestServices = serviceCollection.BuildServiceProvider();

        _httpContextAccessor.HttpContext
            .Returns(httpContext);

        _deviceRepository.GetByIdentifierAsync(_deviceIdentifier)
            .Returns(new Device
            {
                Id = _deviceId,
            });

        var connectTokenRequest = _mockIdentityClient
            .Expect(HttpMethod.Post, "https://localhost:8888/connect/token")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new
            {
                access_token = CreateAccessToken(DateTime.UtcNow.AddDays(1)),
            }));

        var pushSendRequest = _mockPushClient
            .Expect(HttpMethod.Post, "https://localhost:7777/push/send")
            .With(request =>
            {
                if (request.Content is not JsonContent jsonContent)
                {
                    return false;
                }

                // TODO: What options?
                var actualString = JsonSerializer.Serialize(jsonContent.Value);
                var actualNode = JsonNode.Parse(actualString);

                if (!JsonNode.DeepEquals(actualNode, expectedRequestBody))
                {
                    Assert.Equal(expectedRequestBody.ToJsonString(), actualNode.ToJsonString());
                    return false;
                }

                return true;
            })
            .Respond(HttpStatusCode.OK);

        await test(_sut);

        Assert.Equal(1, _mockPushClient.GetMatchCount(pushSendRequest));
    }

    private static string CreateAccessToken(DateTime expirationTime)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(expires: expirationTime);
        return tokenHandler.WriteToken(token);
    }
}
