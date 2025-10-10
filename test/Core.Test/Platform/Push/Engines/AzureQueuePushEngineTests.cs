#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Queues;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Push.Engines;

[QueueClientCustomize]
[SutProviderCustomize]
public class AzureQueuePushEngineTests
{
    private static readonly Guid _deviceId = Guid.Parse("c4730f80-caaa-4772-97bd-5c0d23a2baa3");
    private static readonly string _deviceIdentifier = "test_device_identifier";
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly Core.Settings.GlobalSettings _globalSettings = new();

    public AzureQueuePushEngineTests()
    {
        _fakeTimeProvider = new();
        _fakeTimeProvider.SetUtcNow(DateTime.UtcNow);
    }

    [Theory]
    [InlineData("6a5bbe1b-cf16-49a6-965f-5c2eac56a531", null)]
    [InlineData(null, "b9a3fcb4-2447-45c1-aad2-24de43c88c44")]
    public async Task PushSyncCipherCreateAsync_SendsExpectedResponse(string? userId, string? organizationId)
    {
        var collectionId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = userId != null ? Guid.Parse(userId) : null,
            OrganizationId = organizationId != null ? Guid.Parse(organizationId) : null,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Type"] = 1,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = cipher.OrganizationId,
                ["CollectionIds"] = new JsonArray(collectionId),
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
        };

        if (!cipher.UserId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("UserId");
        }

        if (!cipher.OrganizationId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("OrganizationId");
            expectedPayload["Payload"]!.AsObject().Remove("CollectionIds");
        }

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncCipherCreateAsync(cipher, [collectionId]),
            expectedPayload
        );
    }

    [Theory]
    [InlineData("6a5bbe1b-cf16-49a6-965f-5c2eac56a531", null)]
    [InlineData(null, "b9a3fcb4-2447-45c1-aad2-24de43c88c44")]
    public async Task PushSyncCipherUpdateAsync_SendsExpectedResponse(string? userId, string? organizationId)
    {
        var collectionId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = userId != null ? Guid.Parse(userId) : null,
            OrganizationId = organizationId != null ? Guid.Parse(organizationId) : null,
            RevisionDate = DateTime.UtcNow,
        };

        var expectedPayload = new JsonObject
        {
            ["Type"] = 0,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["OrganizationId"] = cipher.OrganizationId,
                ["CollectionIds"] = new JsonArray(collectionId),
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
        };

        if (!cipher.UserId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("UserId");
        }

        if (!cipher.OrganizationId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("OrganizationId");
            expectedPayload["Payload"]!.AsObject().Remove("CollectionIds");
        }

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
            ["Type"] = 2,
            ["Payload"] = new JsonObject
            {
                ["Id"] = cipher.Id,
                ["UserId"] = cipher.UserId,
                ["RevisionDate"] = cipher.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 7,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 8,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 3,
            ["Payload"] = new JsonObject
            {
                ["Id"] = folder.Id,
                ["UserId"] = folder.UserId,
                ["RevisionDate"] = folder.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 4,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
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
            ["Type"] = 5,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
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
            ["Type"] = 17,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
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
            ["Type"] = 6,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
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
            ["Type"] = 10,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncSettingsAsync(userId),
            expectedPayload
        );
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, LogOutReason.KdfChange)]
    [InlineData(false, null)]
    [InlineData(false, LogOutReason.KdfChange)]
    public async Task PushLogOutAsync_SendsExpectedResponse(bool excludeCurrentContext, LogOutReason? reason)
    {
        var userId = Guid.NewGuid();

        var payload = new JsonObject
        {
            ["UserId"] = userId
        };
        if (reason != null)
        {
            payload["Reason"] = (int)reason;
        }

        var expectedPayload = new JsonObject
        {
            ["Type"] = 11,
            ["Payload"] = payload,
        };

        if (excludeCurrentContext)
        {
            expectedPayload["ContextId"] = _deviceIdentifier;
        }

        await VerifyNotificationAsync(
            async sut => await sut.PushLogOutAsync(userId, excludeCurrentContext, reason),
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
            ["Type"] = 12,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 13,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 14,
            ["Payload"] = new JsonObject
            {
                ["Id"] = send.Id,
                ["UserId"] = send.UserId,
                ["RevisionDate"] = send.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 15,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ContextId"] = _deviceIdentifier,
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
            ["Type"] = 16,
            ["Payload"] = new JsonObject
            {
                ["Id"] = authRequest.Id,
                ["UserId"] = authRequest.UserId,
            },
            ["ContextId"] = _deviceIdentifier,
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

        var expectedPayload = new JsonObject
        {
            ["Type"] = 20,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = global,
                ["ClientType"] = 0,
                ["UserId"] = notification.UserId,
                ["OrganizationId"] = notification.OrganizationId,
                ["InstallationId"] = _globalSettings.Installation.Id,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
            },
            ["ContextId"] = _deviceIdentifier,
        };

        if (!global)
        {
            expectedPayload["Payload"]!.AsObject().Remove("InstallationId");
        }

        if (!notification.UserId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("UserId");
        }

        if (!notification.OrganizationId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("OrganizationId");
        }

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

        var expectedPayload = new JsonObject
        {
            ["Type"] = 21,
            ["Payload"] = new JsonObject
            {
                ["Id"] = notification.Id,
                ["Priority"] = 3,
                ["Global"] = global,
                ["ClientType"] = 0,
                ["UserId"] = notification.UserId,
                ["OrganizationId"] = notification.OrganizationId,
                ["InstallationId"] = _globalSettings.Installation.Id,
                ["Title"] = notification.Title,
                ["Body"] = notification.Body,
                ["CreationDate"] = notification.CreationDate,
                ["RevisionDate"] = notification.RevisionDate,
                ["ReadDate"] = notificationStatus.ReadDate,
                ["DeletedDate"] = notificationStatus.DeletedDate,
            },
            ["ContextId"] = _deviceIdentifier,
        };

        if (!global)
        {
            expectedPayload["Payload"]!.AsObject().Remove("InstallationId");
        }

        if (!notification.UserId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("UserId");
        }

        if (!notification.OrganizationId.HasValue)
        {
            expectedPayload["Payload"]!.AsObject().Remove("OrganizationId");
        }

        await VerifyNotificationAsync(
            async sut => await sut.PushNotificationStatusAsync(notification, notificationStatus),
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
            ["Type"] = 19,
            ["Payload"] = new JsonObject
            {
                ["OrganizationId"] = organization.Id,
                ["LimitCollectionCreation"] = organization.LimitCollectionCreation,
                ["LimitCollectionDeletion"] = organization.LimitCollectionDeletion,
                ["LimitItemDeletion"] = organization.LimitItemDeletion,
            },
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushSyncOrganizationCollectionManagementSettingsAsync(organization),
            expectedPayload
        );
    }

    [Fact]
    public async Task PushRefreshSecurityTasksAsync_SendsExpectedResponse()
    {
        var userId = Guid.NewGuid();

        var expectedPayload = new JsonObject
        {
            ["Type"] = 22,
            ["Payload"] = new JsonObject
            {
                ["UserId"] = userId,
                ["Date"] = _fakeTimeProvider.GetUtcNow().UtcDateTime,
            },
        };

        await VerifyNotificationAsync(
            async sut => await sut.PushRefreshSecurityTasksAsync(userId),
            expectedPayload
        );
    }

    // [Fact]
    // public async Task SendPayloadToInstallationAsync_ThrowsNotImplementedException()
    // {
    //     await Assert.ThrowsAsync<NotImplementedException>(
    //         async () => await sut.SendPayloadToInstallationAsync("installation_id", PushType.AuthRequest, new {}, null)
    //     );
    // }

    // [Fact]
    // public async Task SendPayloadToUserAsync_ThrowsNotImplementedException()
    // {
    //     await Assert.ThrowsAsync<NotImplementedException>(
    //         async () => await _sut.SendPayloadToUserAsync("user_id", PushType.AuthRequest, new {}, null)
    //     );
    // }

    // [Fact]
    // public async Task SendPayloadToOrganizationAsync_ThrowsNotImplementedException()
    // {
    //     await Assert.ThrowsAsync<NotImplementedException>(
    //         async () => await _sut.SendPayloadToOrganizationAsync("organization_id", PushType.AuthRequest, new {}, null)
    //     );
    // }

    private async Task VerifyNotificationAsync(Func<IPushNotificationService, Task> test, JsonNode expectedMessage)
    {
        var queueClient = Substitute.For<QueueClient>();

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        var httpContext = new DefaultHttpContext();

        var serviceCollection = new ServiceCollection();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = _deviceIdentifier;
        serviceCollection.AddSingleton(currentContext);

        httpContext.RequestServices = serviceCollection.BuildServiceProvider();

        httpContextAccessor.HttpContext
            .Returns(httpContext);

        var globalSettings = new Core.Settings.GlobalSettings();

        var sut = new AzureQueuePushEngine(
            queueClient,
            httpContextAccessor,
            globalSettings,
            NullLogger<AzureQueuePushEngine>.Instance
        );

        await test(new EngineWrapper(sut, _fakeTimeProvider, _globalSettings.Installation.Id));

        // Hoist equality checker outside the expression so that we
        // can more easily place a breakpoint
        var checkEquality = (string actual) =>
        {
            var actualNode = JsonNode.Parse(actual);
            return JsonNode.DeepEquals(actualNode, expectedMessage);
        };

        await queueClient
            .Received(1)
            .SendMessageAsync(Arg.Is<string>((actual) => checkEquality(actual)));
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

    private class NotificationPushNotificationEquals(
        Notification notification,
        NotificationStatus? notificationStatus,
        Guid? installationId)
        : IEquatable<NotificationPushNotification>
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
                   other.ClientType == notification.ClientType &&
                   other.InstallationId == installationId &&
                   other.Title == notification.Title &&
                   other.Body == notification.Body &&
                   other.CreationDate == notification.CreationDate &&
                   other.RevisionDate == notification.RevisionDate &&
                   other.ReadDate == notificationStatus?.ReadDate &&
                   other.DeletedDate == notificationStatus?.DeletedDate;
        }
    }
}
