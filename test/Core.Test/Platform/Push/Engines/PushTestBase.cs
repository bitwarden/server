using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Core.Test.Platform.Push.Engines;

public class EngineWrapper(IPushEngine pushEngine, FakeTimeProvider fakeTimeProvider, Guid installationId) : IPushNotificationService
{
    public Guid InstallationId { get; } = installationId;

    public TimeProvider TimeProvider { get; } = fakeTimeProvider;

    public ILogger Logger => NullLogger<EngineWrapper>.Instance;

    public Task PushAsync<T>(PushNotification<T> pushNotification) where T : class
        => pushEngine.PushAsync(pushNotification);

    public Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds)
        => pushEngine.PushCipherAsync(cipher, pushType, collectionIds);
}

public abstract class PushTestBase
{
    protected static readonly string DeviceIdentifier = "test_device_identifier";

    protected readonly MockHttpMessageHandler MockClient = new();
    protected readonly MockHttpMessageHandler MockIdentityClient = new();

    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly GlobalSettings GlobalSettings;
    protected readonly IHttpContextAccessor HttpContextAccessor;
    protected readonly FakeTimeProvider FakeTimeProvider;

    public PushTestBase()
    {
        HttpClientFactory = Substitute.For<IHttpClientFactory>();

        // Mock HttpClient
        HttpClientFactory.CreateClient("client")
            .Returns(new HttpClient(MockClient));

        HttpClientFactory.CreateClient("identity")
            .Returns(new HttpClient(MockIdentityClient));

        GlobalSettings = new GlobalSettings();
        HttpContextAccessor = Substitute.For<IHttpContextAccessor>();

        FakeTimeProvider = new FakeTimeProvider();

        FakeTimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
    }

    protected abstract IPushEngine CreateService();

    protected abstract string ExpectedClientUrl();

    protected abstract JsonNode GetPushSyncCipherCreatePayload(Cipher cipher, Guid collectionId);
    protected abstract JsonNode GetPushSyncCipherUpdatePayload(Cipher cipher, Guid collectionId);
    protected abstract JsonNode GetPushSyncCipherDeletePayload(Cipher cipher);
    protected abstract JsonNode GetPushSyncFolderCreatePayload(Folder folder);
    protected abstract JsonNode GetPushSyncFolderUpdatePayload(Folder folder);
    protected abstract JsonNode GetPushSyncFolderDeletePayload(Folder folder);
    protected abstract JsonNode GetPushSyncCiphersPayload(Guid userId);
    protected abstract JsonNode GetPushSyncVaultPayload(Guid userId);
    protected abstract JsonNode GetPushSyncOrganizationsPayload(Guid userId);
    protected abstract JsonNode GetPushSyncOrgKeysPayload(Guid userId);
    protected abstract JsonNode GetPushSyncSettingsPayload(Guid userId);
    protected abstract JsonNode GetPushLogOutPayload(Guid userId, bool excludeCurrentContext);
    protected abstract JsonNode GetPushSendCreatePayload(Send send);
    protected abstract JsonNode GetPushSendUpdatePayload(Send send);
    protected abstract JsonNode GetPushSendDeletePayload(Send send);
    protected abstract JsonNode GetPushAuthRequestPayload(AuthRequest authRequest);
    protected abstract JsonNode GetPushAuthRequestResponsePayload(AuthRequest authRequest);
    protected abstract JsonNode GetPushNotificationResponsePayload(Notification notification, Guid? userId, Guid? organizationId);
    protected abstract JsonNode GetPushNotificationStatusResponsePayload(Notification notification, NotificationStatus notificationStatus, Guid? userId, Guid? organizationId);
    protected abstract JsonNode GetPushSyncOrganizationStatusResponsePayload(Organization organization);
    protected abstract JsonNode GetPushSyncOrganizationCollectionManagementSettingsResponsePayload(Organization organization);
    protected abstract JsonNode GetPushRefreshSecurityTasksResponsePayload(Guid userId);

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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherCreateAsync(cipher, [collectionId]),
            GetPushSyncCipherCreatePayload(cipher, collectionId)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherUpdateAsync(cipher, [collectionId]),
            GetPushSyncCipherUpdatePayload(cipher, collectionId)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherDeleteAsync(cipher),
            GetPushSyncCipherDeletePayload(cipher)
        );
    }

    [Fact]
    public async Task PushSyncFolderCreateAsync_SendsExpectedResponse()
    {
        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderCreateAsync(folder),
            GetPushSyncFolderCreatePayload(folder)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderUpdateAsync(folder),
            GetPushSyncFolderUpdatePayload(folder)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncFolderDeleteAsync(folder),
            GetPushSyncFolderDeletePayload(folder)
        );
    }

    [Fact]
    public async Task PushSyncCiphersAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCiphersAsync(userId),
            GetPushSyncCiphersPayload(userId)
        );
    }

    [Fact]
    public async Task PushSyncVaultAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncVaultAsync(userId),
            GetPushSyncVaultPayload(userId)
        );
    }

    [Fact]
    public async Task PushSyncOrganizationsAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationsAsync(userId),
            GetPushSyncOrganizationsPayload(userId)
        );
    }

    [Fact]
    public async Task PushSyncOrgKeysAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrgKeysAsync(userId),
            GetPushSyncOrgKeysPayload(userId)
        );
    }

    [Fact]
    public async Task PushSyncSettingsAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSettingsAsync(userId),
            GetPushSyncSettingsPayload(userId)
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PushLogOutAsync_SendsExpectedResponse(bool excludeCurrentContext)
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushLogOutAsync(userId, excludeCurrentContext),
            GetPushLogOutPayload(userId, excludeCurrentContext)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendCreateAsync(send),
            GetPushSendCreatePayload(send)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendUpdateAsync(send),
            GetPushSendUpdatePayload(send)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSendDeleteAsync(send),
            GetPushSendDeletePayload(send)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushAuthRequestAsync(authRequest),
            GetPushAuthRequestPayload(authRequest)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushAuthRequestResponseAsync(authRequest),
            GetPushAuthRequestResponsePayload(authRequest)
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
            TaskId = Guid.NewGuid(),
            Title = "My Title",
            Body = "My Body",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            RevisionDate = DateTime.UtcNow,
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationAsync(notification),
            GetPushNotificationResponsePayload(notification, notification.UserId, notification.OrganizationId)
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
            TaskId = Guid.NewGuid(),
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

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationStatusAsync(notification, notificationStatus),
            GetPushNotificationStatusResponsePayload(notification, notificationStatus, notification.UserId, notification.OrganizationId)
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

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationCollectionManagementSettingsAsync(organization),
            GetPushSyncOrganizationCollectionManagementSettingsResponsePayload(organization)
        );
    }

    [Fact]
    public async Task PushRefreshSecurityTasksAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        await VerifyNotificationAsync(
            async sut => await sut.PushRefreshSecurityTasksAsync(userId),
            GetPushRefreshSecurityTasksResponsePayload(userId)
        );
    }

    private async Task VerifyNotificationAsync(
        Func<IPushNotificationService, Task> test,
        JsonNode expectedRequestBody
    )
    {
        var httpContext = new DefaultHttpContext();

        var serviceCollection = new ServiceCollection();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = DeviceIdentifier;
        serviceCollection.AddSingleton(currentContext);

        httpContext.RequestServices = serviceCollection.BuildServiceProvider();

        HttpContextAccessor.HttpContext
            .Returns(httpContext);

        var connectTokenRequest = MockIdentityClient
            .Expect(HttpMethod.Post, "https://localhost:8888/connect/token")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new
            {
                access_token = CreateAccessToken(DateTime.UtcNow.AddDays(1)),
            }));

        JsonNode actualNode = null;

        var clientRequest = MockClient
            .Expect(HttpMethod.Post, ExpectedClientUrl())
            .With(request =>
            {
                if (request.Content is not JsonContent jsonContent)
                {
                    return false;
                }

                // TODO: What options?
                var actualString = JsonSerializer.Serialize(jsonContent.Value);
                actualNode = JsonNode.Parse(actualString);

                return JsonNode.DeepEquals(actualNode, expectedRequestBody);
            })
            .Respond(HttpStatusCode.OK);

        await test(new EngineWrapper(CreateService(), FakeTimeProvider, GlobalSettings.Installation.Id));

        Assert.NotNull(actualNode);

        Assert.Equal(expectedRequestBody, actualNode, EqualityComparer<JsonNode>.Create(JsonNode.DeepEquals));

        Assert.Equal(1, MockClient.GetMatchCount(clientRequest));
    }

    protected static string CreateAccessToken(DateTime expirationTime)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(expires: expirationTime);
        return tokenHandler.WriteToken(token);
    }
}
