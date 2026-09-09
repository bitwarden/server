#nullable enable
using System.Text.Json.Nodes;
using Azure.Storage.Queues;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Push.Engines;

public class AzureQueuePushEngineTests
{
    private const string DeviceIdentifier = "test_device_identifier";

    [Fact]
    public async Task PushAsync_SerializesEnvelope()
    {
        var userId = Guid.NewGuid();
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var (sut, queueClient) = BuildSut();

        await sut.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification { UserId = userId, Date = date },
            ExcludeCurrentContext = false,
        });

        var message = CaptureMessage(queueClient);
        Assert.Equal((byte)PushType.SyncCiphers, (byte?)message["Type"]);
        Assert.Equal(userId, (Guid?)message["Payload"]?["UserId"]);
        Assert.Equal(date, (DateTime?)message["Payload"]?["Date"]);
        Assert.Null(message["ContextId"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PushAsync_ExcludeCurrentContext_ControlsContextId(bool excludeCurrentContext)
    {
        var userId = Guid.NewGuid();
        var (sut, queueClient) = BuildSut();

        await sut.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification { UserId = userId, Date = DateTime.UtcNow },
            ExcludeCurrentContext = excludeCurrentContext,
        });

        var message = CaptureMessage(queueClient);
        if (excludeCurrentContext)
        {
            Assert.Equal(DeviceIdentifier, (string?)message["ContextId"]);
        }
        else
        {
            Assert.Null(message["ContextId"]);
        }
    }

    private (AzureQueuePushEngine, QueueClient) BuildSut()
    {
        var queueClient = Substitute.For<QueueClient>();

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = DeviceIdentifier;
        services.AddSingleton(currentContext);
        httpContext.RequestServices = services.BuildServiceProvider();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var sut = new AzureQueuePushEngine(
            queueClient,
            httpContextAccessor,
            new Core.Settings.GlobalSettings(),
            NullLogger<AzureQueuePushEngine>.Instance
        );

        return (sut, queueClient);
    }

    private static JsonObject CaptureMessage(QueueClient queueClient)
    {
        var call = queueClient.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(QueueClient.SendMessageAsync));
        return JsonNode.Parse((string)call.GetArguments()[0]!)!.AsObject();
    }
}
