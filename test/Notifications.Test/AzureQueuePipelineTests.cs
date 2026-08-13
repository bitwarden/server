using System.Text.Json.Nodes;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Settings;
using Bit.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Notifications.Test;

/// <summary>
/// Tests for the Azure Queue push pipeline.
///
/// Two contracts are enforced here:
/// <list type="number">
///   <item>Every format in <see cref="SupportedPayloads"/> must be accepted by
///         <see cref="AzureQueueHostedService"/> and routed to the correct SignalR hub group.</item>
///   <item>Whatever <see cref="AzureQueuePushEngine.PushAsync"/> currently produces must
///         be one of those formats, so any wire-format change is caught immediately.</item>
/// </list>
/// When <c>PushAsync</c> is updated to produce a new shape, add the new entry to
/// <see cref="RoutingCases"/>. <see cref="SupportedPayloads"/> is derived from it automatically.
///
/// <para><strong>Not every push type is covered intentionally.</strong> The long-term goal is to
/// move routing decisions to envelope-level fields rather than inner-payload inspection. Once
/// that migration is complete the payload becomes opaque and exhaustive per-type coverage would
/// add noise. The representative sample in <see cref="SupportedPayloads"/> is sufficient to guard
/// the contract until then.</para>
/// </summary>
public sealed class AzureQueuePipelineTests : IAsyncLifetime
{
    // Fixed IDs used in all payload literals below.
    private static readonly Guid _userId = Guid.Parse("d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c");
    private static readonly Guid _orgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _installationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _notifId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid _authRequestId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const string TestContextId = "test-device-id";

    // Each supported Azure Queue wire format paired with the SignalR routing call it must trigger.
    //
    // Azure Queue wire format: PascalCase property names, null values absent
    // (AzureQueuePushEngine serializes with JsonHelpers.IgnoreWritingNull).
    // This differs from the POST /send format (camelCase, explicit nulls).
    //
    // When PushAsync changes its wire format, add the new entry here.
    // Old entries must be kept for at least one release to support rolling upgrades.
    //
    // Do not add a new entry in the same commit that updates AzureQueueHostedService to
    // handle it — see PostSendEndpointTests for the full rationale.
    private sealed record RoutingCase(string Json, string? ExpectedUserId, string? ExpectedGroup, string? ExpectedAnonymousGroup = null);

    private static readonly RoutingCase[] RoutingCases =
    [
        // User — LogOut, no context exclusion (Reason and ContextId absent — IgnoreWritingNull)
        new("""{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}}""",
            _userId.ToString(), null),
        // User — LogOut, with context exclusion
        new("""{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"},"ContextId":"test-device-id"}""",
            _userId.ToString(), null),
        // Organization — SyncOrganizationStatusChanged, no context exclusion
        new("""{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true}}""",
            null, NotificationsHub.GetOrganizationGroup(_orgId)),
        // Organization — SyncOrganizationStatusChanged, with context exclusion
        new("""{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":"test-device-id"}""",
            null, NotificationsHub.GetOrganizationGroup(_orgId)),
        // Installation — Notification (ClientType.All), no context exclusion
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.All)),
        // Installation — Notification (ClientType.All), with context exclusion
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"},"ContextId":"test-device-id"}""",
            null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.All)),
        // User — Notification (ClientType.Browser), routes to client-type-specific group
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetUserGroup(_userId, ClientType.Browser)),
        // Organization — Notification (ClientType.Browser), routes to client-type-specific group
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetOrganizationGroup(_orgId, ClientType.Browser)),
        // Installation — Notification (ClientType.Browser), routes to client-type-specific group
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.Browser)),
        // AuthRequestResponse — routes to anonymous hub Group(authRequestId)
        new("""{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id"}""",
            null, null, _authRequestId.ToString()),
    ];

    private static readonly string[] SupportedPayloads = RoutingCases.Select(c => c.Json).ToArray();

    private readonly ChannelQueueClient _queue = new();
    private readonly IClientProxy _proxy;
    private readonly IHubClients _hubClients;
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IClientProxy _anonymousProxy;
    private readonly IHubClients _anonymousHubClients;
    private readonly IHost _host;

    public AzureQueuePipelineTests()
    {
        _proxy = Substitute.For<IClientProxy>();
        _hubClients = Substitute.For<IHubClients>();
        _hubClients.User(Arg.Any<string>()).Returns(_proxy);
        _hubClients.Group(Arg.Any<string>()).Returns(_proxy);

        _hubContext = Substitute.For<IHubContext<NotificationsHub>>();
        _hubContext.Clients.Returns(_hubClients);

        _anonymousProxy = Substitute.For<IClientProxy>();
        _anonymousHubClients = Substitute.For<IHubClients>();
        _anonymousHubClients.Group(Arg.Any<string>()).Returns(_anonymousProxy);

        var anonymousHubContext = Substitute.For<IHubContext<AnonymousNotificationsHub>>();
        anonymousHubContext.Clients.Returns(_anonymousHubClients);

        // Non-empty ConnectionString so StartAsync's guard passes.
        // The real QueueClient is never created because _queue is injected via keyed registration.
        var globalSettings = new GlobalSettings();
        globalSettings.Notifications.ConnectionString = "fake-connection-string";

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddKeyedSingleton<Azure.Storage.Queues.QueueClient>("notifications", _queue);
                services.AddSingleton(globalSettings);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider());
                services.AddSingleton(_hubContext);
                services.AddSingleton(anonymousHubContext);
                services.AddSingleton<HubHelpers>();
                services.AddSingleton<AzureQueueHostedService>();
                services.AddHostedService(sp => sp.GetRequiredService<AzureQueueHostedService>());
            })
            .UseConsoleLifetime()
            .Build();
    }

    public async Task InitializeAsync() => await _host.StartAsync();

    public async Task DisposeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _host.StopAsync(cts.Token);
        _host.Dispose();
    }

    /// <summary>All (target, excludeCurrentContext) combinations exercised against the engine.</summary>
    public static IEnumerable<object[]> EngineInputArgs() =>
        from target in new[] { NotificationTarget.User, NotificationTarget.Organization, NotificationTarget.Installation }
        from excludeCurrentContext in new[] { false, true }
        select new object[] { target, excludeCurrentContext };

    /// <summary>All targets for the Notification push type with <see cref="ClientType.Browser"/>.</summary>
    public static IEnumerable<object[]> NotificationClientTypeArgs() =>
        from target in Enum.GetValues<NotificationTarget>()
        select new object[] { target, ClientType.Browser };

    public static IEnumerable<object?[]> RoutingCaseArgs() =>
        RoutingCases.Select(c => new object?[] { c.Json, c.ExpectedUserId, c.ExpectedGroup, c.ExpectedAnonymousGroup });

    /// <summary>
    /// Verifies that the JSON currently produced by <see cref="AzureQueuePushEngine.PushAsync"/>
    /// for every (target, context) combination is represented in <see cref="SupportedPayloads"/>.
    /// Fails when <c>PushAsync</c> changes its wire format without a corresponding update.
    /// </summary>
    [Theory]
    [MemberData(nameof(EngineInputArgs))]
    public async Task PushAsync_ProducesASupportedPayload(NotificationTarget target, bool excludeCurrentContext)
    {
        var captured = await CapturePayloadAsync(excludeCurrentContext,
            engine => PushForTargetAsync(engine, target, excludeCurrentContext));
        var capturedNode = JsonNode.Parse(captured);

        Assert.True(
            SupportedPayloads.Any(s => JsonNode.DeepEquals(capturedNode, JsonNode.Parse(s))),
            $"AzureQueuePushEngine.PushAsync produced a payload not listed in {nameof(SupportedPayloads)}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Supported:\n  {string.Join("\n  ", SupportedPayloads)}");
    }

    /// <summary>
    /// Verifies that every format in <see cref="SupportedPayloads"/> is accepted by
    /// <see cref="AzureQueueHostedService"/> and routed to the correct SignalR user or group.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoutingCaseArgs))]
    public async Task RouteFromQueue_RoutesPayloadToCorrectHubGroup(
        string json, string? expectedUserId, string? expectedGroup, string? expectedAnonymousGroup)
    {
        var targetProxy = expectedAnonymousGroup is not null ? _anonymousProxy : _proxy;
        var tcs = new TaskCompletionSource();
        targetProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => { tcs.TrySetResult(); return Task.CompletedTask; });

        await _queue.SendMessageAsync(json);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (expectedUserId is not null)
        {
            _hubClients.Received(1).User(expectedUserId);
        }
        else if (expectedGroup is not null)
        {
            _hubClients.Received(1).Group(expectedGroup);
        }
        else
        {
            _anonymousHubClients.Received(1).Group(expectedAnonymousGroup!);
        }
    }

    /// <summary>
    /// Verifies the full chain: <see cref="AzureQueuePushEngine.PushAsync"/> for
    /// <see cref="PushType.Notification"/> with a specific <see cref="ClientType"/> produces a
    /// wire format that the hosted service accepts and routes to the correct client-type-scoped
    /// SignalR group.
    /// </summary>
    [Theory]
    [MemberData(nameof(NotificationClientTypeArgs))]
    public async Task PushAsync_Notification_RoutesToClientTypeGroup(
        NotificationTarget target, ClientType clientType)
    {
        var captured = await CapturePayloadAsync(false,
            engine => engine.PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.Notification,
                Target = target,
                TargetId = target switch
                {
                    NotificationTarget.User => _userId,
                    NotificationTarget.Organization => _orgId,
                    NotificationTarget.Installation => _installationId,
                    _ => throw new ArgumentOutOfRangeException(nameof(target)),
                },
                Payload = new NotificationPushNotification
                {
                    Id = _notifId,
                    UserId = target == NotificationTarget.User ? _userId : null,
                    OrganizationId = target == NotificationTarget.Organization ? _orgId : null,
                    InstallationId = target == NotificationTarget.Installation ? _installationId : null,
                    ClientType = clientType,
                },
                ExcludeCurrentContext = false,
            }));

        Assert.True(
            SupportedPayloads.Any(s => JsonNode.DeepEquals(JsonNode.Parse(captured), JsonNode.Parse(s))),
            $"Notification payload with {nameof(ClientType)}.{clientType} for {target} not in {nameof(SupportedPayloads)}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Supported:\n  {string.Join("\n  ", SupportedPayloads)}");

        var tcs = new TaskCompletionSource();
        _proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => { tcs.TrySetResult(); return Task.CompletedTask; });

        await _queue.SendMessageAsync(captured);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var expectedGroup = target switch
        {
            NotificationTarget.User => NotificationsHub.GetUserGroup(_userId, clientType),
            NotificationTarget.Organization => NotificationsHub.GetOrganizationGroup(_orgId, clientType),
            NotificationTarget.Installation => NotificationsHub.GetInstallationGroup(_installationId, clientType),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        _hubClients.Received(1).Group(expectedGroup);
    }

    // Creates a fresh ChannelQueueClient (separate from _queue), builds an AzureQueuePushEngine
    // with it, calls invoke, and returns the JSON the engine wrote to the queue.
    private static async Task<string> CapturePayloadAsync(
        bool excludeCurrentContext, Func<AzureQueuePushEngine, Task> invoke)
    {
        var captureQueue = new ChannelQueueClient();

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        if (excludeCurrentContext)
        {
            var currentContext = Substitute.For<ICurrentContext>();
            currentContext.DeviceIdentifier.Returns(TestContextId);
            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(ICurrentContext)).Returns(currentContext);
            var httpContext = Substitute.For<HttpContext>();
            httpContext.RequestServices.Returns(serviceProvider);
            httpContextAccessor.HttpContext.Returns(httpContext);
        }

        var globalSettings = new GlobalSettings { Installation = { Id = _installationId } };
        var engine = new AzureQueuePushEngine(
            captureQueue,
            httpContextAccessor,
            globalSettings,
            NullLogger<AzureQueuePushEngine>.Instance);

        await invoke(engine);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await captureQueue.AwaitNextSentAsync(cts.Token);
    }

    private static Task PushForTargetAsync(
        AzureQueuePushEngine engine, NotificationTarget target, bool excludeCurrentContext) =>
        target switch
        {
            NotificationTarget.User => engine.PushAsync(new PushNotification<LogOutPushNotification>
            {
                Type = PushType.LogOut,
                Target = target,
                TargetId = _userId,
                Payload = new LogOutPushNotification { UserId = _userId },
                ExcludeCurrentContext = excludeCurrentContext,
            }),
            NotificationTarget.Organization => engine.PushAsync(new PushNotification<OrganizationStatusPushNotification>
            {
                Type = PushType.SyncOrganizationStatusChanged,
                Target = target,
                TargetId = _orgId,
                Payload = new OrganizationStatusPushNotification { OrganizationId = _orgId, Enabled = true },
                ExcludeCurrentContext = excludeCurrentContext,
            }),
            NotificationTarget.Installation => engine.PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.Notification,
                Target = target,
                TargetId = _installationId,
                Payload = new NotificationPushNotification
                {
                    Id = _notifId,
                    InstallationId = _installationId,
                    ClientType = ClientType.All,
                },
                ExcludeCurrentContext = excludeCurrentContext,
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
}
