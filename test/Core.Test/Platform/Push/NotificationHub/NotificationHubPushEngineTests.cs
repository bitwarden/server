using System.Text.Json.Nodes;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Push.NotificationHub;

public class NotificationHubPushEngineTests
{
    private const string DeviceIdentifier = "test_device_identifier";

    [Fact]
    public async Task PushAsync_SerializesEnvelope()
    {
        var userId = Guid.NewGuid();
        var (sut, proxy) = BuildSut();

        await sut.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification { UserId = userId, Date = DateTime.UtcNow },
            ExcludeCurrentContext = false,
        });

        var (dict, _) = CaptureCall(proxy);
        Assert.Equal(((byte)PushType.SyncCiphers).ToString(), dict["type"]);
        var payload = JsonNode.Parse(dict["payload"])!;
        Assert.Equal(userId, (Guid?)payload["UserId"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PushAsync_UserTarget_ExcludeCurrentContext_ControlsTag(bool excludeCurrentContext)
    {
        var userId = Guid.NewGuid();
        var (sut, proxy) = BuildSut();

        await sut.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification { UserId = userId, Date = DateTime.UtcNow },
            ExcludeCurrentContext = excludeCurrentContext,
        });

        var (_, tag) = CaptureCall(proxy);
        var expected = excludeCurrentContext
            ? $"(template:payload_userId:{userId} && !deviceIdentifier:{DeviceIdentifier})"
            : $"(template:payload_userId:{userId})";
        Assert.Equal(expected, tag);
    }

    [Fact]
    public async Task PushAsync_OrganizationTarget_BuildsExpectedTag()
    {
        var orgId = Guid.NewGuid();
        var (sut, proxy) = BuildSut();

        await sut.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.Organization,
            TargetId = orgId,
            Payload = new UserPushNotification { UserId = Guid.NewGuid(), Date = DateTime.UtcNow },
            ExcludeCurrentContext = true,
        });

        var (_, tag) = CaptureCall(proxy);
        Assert.Equal($"(template:payload && organizationId:{orgId} && !deviceIdentifier:{DeviceIdentifier})", tag);
    }

    [Fact]
    public async Task PushAsync_InstallationTarget_BuildsExpectedTag()
    {
        var installationId = Guid.NewGuid();
        var (sut, proxy) = BuildSut(installationId);

        await sut.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.Installation,
            TargetId = installationId,
            Payload = new UserPushNotification { UserId = Guid.NewGuid(), Date = DateTime.UtcNow },
            ExcludeCurrentContext = true,
        });

        var (_, tag) = CaptureCall(proxy);
        Assert.Equal($"(template:payload && installationId:{installationId} && !deviceIdentifier:{DeviceIdentifier})", tag);
    }

    private (NotificationHubPushEngine, INotificationHubProxy) BuildSut(Guid? installationId = null)
    {
        var notificationHubPool = Substitute.For<INotificationHubPool>();
        var notificationHubProxy = Substitute.For<INotificationHubProxy>();
        notificationHubPool.AllClients.Returns(notificationHubProxy);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier = DeviceIdentifier;
        services.AddSingleton(currentContext);
        httpContext.RequestServices = services.BuildServiceProvider();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var globalSettings = new Core.Settings.GlobalSettings();
        if (installationId.HasValue)
        {
            globalSettings.Installation.Id = installationId.Value;
        }

        var sut = new NotificationHubPushEngine(
            Substitute.For<IInstallationDeviceRepository>(),
            notificationHubPool,
            httpContextAccessor,
            NullLogger<NotificationHubPushEngine>.Instance,
            globalSettings
        );

        return (sut, notificationHubProxy);
    }

    private static (Dictionary<string, string> dict, string tag) CaptureCall(INotificationHubProxy proxy)
    {
        var methodInfo = typeof(INotificationHubProxy).GetMethod(nameof(INotificationHubProxy.SendTemplateNotificationAsync));
        var call = Assert.Single(proxy.ReceivedCalls(), c => c.GetMethodInfo() == methodInfo);
        var args = call.GetArguments();
        return ((Dictionary<string, string>)args[0]!, (string)args[1]!);
    }
}
