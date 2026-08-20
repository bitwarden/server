using System.Net;
using System.Text;
using Azure.Storage.Queues;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Notifications;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Notifications.Test;

/// <summary>
/// Pins the MessagePack shape of a notification as clients receive it, driven from both ingress
/// paths into the Notifications service.
///
/// <para>The two ingress paths disagree on JSON conventions: the Azure Queue path
/// (<see cref="Bit.Core.Platform.Push.Internal.AzureQueuePushEngine"/> →
/// <see cref="AzureQueueHostedService"/>) writes PascalCase property names and omits nulls, while
/// <c>POST /send</c> (<see cref="Bit.Core.Platform.Push.Internal.NotificationsApiPushEngine"/>)
/// sends camelCase property names with explicit nulls. Both are funnelled through
/// <see cref="HubHelpers.SendNotificationToHubAsync"/>, which deserializes case-insensitively into
/// the same CLR types, so the shape SignalR puts on the wire is derived from those types and not
/// from the ingress JSON.</para>
///
/// <para>That is the contract these tests enforce: the frame is byte-identical across ingress
/// paths, and its decoded shape matches a pinned literal. A renamed or reordered property, a
/// property added to a payload type, a different MessagePack resolver in <c>Startup</c>, or a
/// changed client method name all fail here. Changing an expected literal is therefore a deliberate
/// statement that every client can handle the new shape.</para>
///
/// <para><strong>These assertions are stricter than what would actually break a client.</strong>
/// The resolver produces string-keyed maps, so clients decode by property name: reordering
/// properties, or adding one clients ignore, changes these bytes without breaking any real
/// consumer. The strictness is the point — it makes every change to the client-facing shape
/// visible in review rather than silent — but when a literal here needs updating, the question to
/// answer is whether clients can handle the new shape, not whether the bytes moved.</para>
///
/// <para><strong>Not every push type is covered intentionally.</strong> The shape is a function of
/// the payload CLR type rather than of the push type, so the representative sample below covers the
/// encodings that matter: <see cref="Guid"/>, enums, booleans, strings, both hubs, every nullable
/// kind in both its null and non-null form, and — because MessagePack selects a timestamp format
/// per value rather than per type — <see cref="DateTime"/> values that land in each format
/// production can produce.</para>
/// </summary>
public sealed class MessagePackWireShapeTests : IAsyncLifetime
{
    // Fixed IDs, shared with AzureQueuePipelineTests and PostSendEndpointTests so the JSON
    // literals below can be compared against the ones those tests pin for each ingress path.
    private static readonly Guid _userId = Guid.Parse("d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c");
    private static readonly Guid _orgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _installationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _authRequestId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    /// <summary>
    /// One logical notification expressed in both ingress formats, together with the routing and
    /// MessagePack shape it must produce.
    /// </summary>
    /// <param name="Name">Identifies the case in test output and in <c>MemberData</c>.</param>
    /// <param name="QueueJson">The Azure Queue format: PascalCase, nulls omitted.</param>
    /// <param name="SendJson">The <c>POST /send</c> format: camelCase, explicit nulls.</param>
    /// <param name="ExpectedHub">Name of the hub type the notification must be routed through.</param>
    /// <param name="ExpectedDestination">The routed destination, as <c>User:{id}</c> or <c>Group:{name}</c>.</param>
    /// <param name="ExpectedMethod">The client method name, part of the wire contract.</param>
    /// <param name="ExpectedMessagePackJson">
    /// The decoded MessagePack frame: <c>[messageType, headers, invocationId, target, [arguments], streamIds]</c>.
    /// Readable, but lossy: JSON has no timestamp type, so a <see cref="DateTime"/> encoded as a
    /// MessagePack extension renders identically to an ISO string. <paramref name="ExpectedFrameHex"/>
    /// is what actually pins the encoding.
    /// </param>
    /// <param name="ExpectedFrameHex">
    /// The exact frame, length prefix included. Unlike the decoded form this distinguishes every
    /// MessagePack type, so it catches a value that keeps its rendering while changing its encoding.
    /// </param>
    private sealed record WireCase(
        string Name,
        string QueueJson,
        string SendJson,
        string ExpectedHub,
        string ExpectedDestination,
        string ExpectedMethod,
        string ExpectedMessagePackJson,
        string ExpectedFrameHex);

    private static readonly WireCase[] WireCases =
    [
        new(
            "LogOut/User",
            """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}}""",
            """{"type":11,"payload":{"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","reason":null},"contextId":null}""",
            nameof(NotificationsHub),
            $"User:{_userId}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":null}],[]]""",
            "65960180C0AE526563656976654D6573736167659183A4547970650BA75061796C6F616482A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763A6526561736F6EC0A9436F6E746578744964C090"),
        new(
            "SyncOrganizationStatusChanged/Organization",
            """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true}}""",
            """{"type":18,"payload":{"organizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","enabled":true},"contextId":null}""",
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetOrganizationGroup(_orgId)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":null}],[]]""",
            "6E960180C0AE526563656976654D6573736167659183A45479706512A75061796C6F616482AE4F7267616E697A6174696F6E4964D92461616161616161612D616161612D616161612D616161612D616161616161616161616161A7456E61626C6564C3A9436F6E746578744964C090"),
        new(
            "Notification/Installation",
            """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":0,"global":false,"clientType":0,"userId":null,"organizationId":null,"installationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","taskId":null,"title":null,"body":null,"creationDate":"0001-01-01T00:00:00","revisionDate":"0001-01-01T00:00:00","readDate":null,"deletedDate":null},"contextId":null}""",
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetInstallationGroup(_installationId, ClientType.All)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00.0000000Z","RevisionDate":"0001-01-01T00:00:00.0000000Z","ReadDate":null,"DeletedDate":null},"ContextId":null}],[]]""",
            // Both dates are DateTime.MinValue: C70CFF… is ext8 / timestamp96, the 15-byte form
            // reserved for out-of-range seconds. Production values never reach it — see the case below.
            "A802960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747900A6476C6F62616CC2AA436C69656E745479706500A6557365724964C0AE4F7267616E697A6174696F6E4964C0AE496E7374616C6C6174696F6E4964D92462626262626262622D626262622D626262622D626262622D626262626262626262626262A65461736B4964C0A55469746C65C0A4426F6479C0AC4372656174696F6E44617465C70CFF00000000FFFFFFF1886E0900AC5265766973696F6E44617465C70CFF00000000FFFFFFF1886E0900A85265616444617465C0AB44656C6574656444617465C0A9436F6E746578744964C090"),
        // Realistic values, unlike the cases above which mirror the fixtures pinned by
        // AzureQueuePipelineTests and PostSendEndpointTests. MessagePack picks a timestamp format
        // per value, so DateTime.MinValue — the only date those fixtures carry — encodes as the one
        // format production never produces. This case pins the two that actually occur, along with
        // a populated nullable date and non-null strings.
        new(
            "Notification/User/RealisticValues",
            """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":3,"Global":false,"ClientType":0,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","TaskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","Title":"Test title","Body":"Test body","CreationDate":"2026-08-20T12:34:56Z","RevisionDate":"2026-08-20T12:34:56.1234567Z","ReadDate":"2026-08-21T08:00:00Z"}}""",
            """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":3,"global":false,"clientType":0,"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","organizationId":null,"installationId":null,"taskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","title":"Test title","body":"Test body","creationDate":"2026-08-20T12:34:56Z","revisionDate":"2026-08-20T12:34:56.1234567Z","readDate":"2026-08-21T08:00:00Z","deletedDate":null},"contextId":null}""",
            nameof(NotificationsHub),
            $"User:{_userId}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":3,"Global":false,"ClientType":0,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","Title":"Test title","Body":"Test body","CreationDate":"2026-08-20T12:34:56.0000000Z","RevisionDate":"2026-08-20T12:34:56.1234567Z","ReadDate":"2026-08-21T08:00:00.0000000Z","DeletedDate":null},"ContextId":null}],[]]""",
            // D6FF… is fixext4 / timestamp32 (whole seconds), D7FF… is fixext8 / timestamp64
            // (sub-second precision). These are the two forms real notification dates take, and
            // neither is distinguishable from an ISO string in the decoded form above.
            "D702960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747903A6476C6F62616CC2AA436C69656E745479706500A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763AE4F7267616E697A6174696F6E4964C0AE496E7374616C6C6174696F6E4964C0A65461736B4964D92465656565656565652D656565652D656565652D656565652D656565656565656565656565A55469746C65AA54657374207469746C65A4426F6479A95465737420626F6479AC4372656174696F6E44617465D6FF6A86F470AC5265766973696F6E44617465D7FF1D6F32F06A86F470A85265616444617465D6FF6A880580AB44656C6574656444617465C0A9436F6E746578744964C090"),
        new(
            "AuthRequestResponse/AnonymousHub",
            """{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id"}""",
            """{"type":16,"payload":{"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"contextId":"test-device-id"}""",
            nameof(AnonymousNotificationsHub),
            $"Group:{_authRequestId}",
            "AuthRequestResponseRecieved",
            """[1,{},null,"AuthRequestResponseRecieved",[{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id"}],[]]""",
            "A101960180C0BB4175746852657175657374526573706F6E736552656369657665649183A45479706510A75061796C6F616482A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763A24964D92464646464646464642D646464642D646464642D646464642D646464646464646464646464A9436F6E746578744964AE746573742D6465766963652D696490"),
    ];

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly NotificationsApplicationFactory _factory = new();
    private readonly ChannelQueueClient _queue = new();
    private readonly HubInvocationRecorder _queueRecorder = new();
    private readonly IHost _queueHost;

    public MessagePackWireShapeTests()
    {
        // The Azure Queue consumer and POST /send cannot run in the same host: the consumer bails
        // out when SelfHosted is true, which is exactly when SendController is enabled. So the
        // queue side is hosted separately here while the endpoint side runs in the real service via
        // NotificationsApplicationFactory. Both sides encode through the service's own hub protocol,
        // so Startup remains the only definition of the wire format.
        var globalSettings = new GlobalSettings();
        globalSettings.Notifications.ConnectionString = "fake-connection-string";

        var (notificationsHubContext, _) = _queueRecorder.CreateHubContext<NotificationsHub>();
        var (anonymousHubContext, _) = _queueRecorder.CreateHubContext<AnonymousNotificationsHub>();

        _queueHost = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddKeyedSingleton<QueueClient>("notifications", _queue);
                services.AddSingleton(globalSettings);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider());
                services.AddSingleton(notificationsHubContext);
                services.AddSingleton(anonymousHubContext);
                services.AddSingleton<HubHelpers>();
                services.AddHostedService<AzureQueueHostedService>();
            })
            .UseConsoleLifetime()
            .Build();
    }

    public async Task InitializeAsync() => await _queueHost.StartAsync();

    public async Task DisposeAsync()
    {
        using var cts = new CancellationTokenSource(_timeout);
        await _queueHost.StopAsync(cts.Token);
        _queueHost.Dispose();
        await _factory.DisposeAsync();
    }

    public static IEnumerable<object[]> WireCaseNames() => WireCases.Select(c => new object[] { c.Name });

    /// <summary>
    /// Verifies that the same notification arriving over the Azure Queue and over <c>POST /send</c>
    /// leaves the service as the same MessagePack frame, and that the frame matches its pinned shape.
    /// </summary>
    [Theory]
    [MemberData(nameof(WireCaseNames))]
    public async Task Notification_HasSameMessagePackShape_FromEitherIngressPath(string caseName)
    {
        var wireCase = WireCases.Single(c => c.Name == caseName);

        // Guards the premise of the test: the two ingress paths really do disagree on JSON casing.
        Assert.NotEqual(wireCase.QueueJson, wireCase.SendJson);

        var fromQueue = await RouteThroughQueueAsync(wireCase.QueueJson);
        var fromSendEndpoint = await RouteThroughSendEndpointAsync(wireCase.SendJson);

        AssertRoutedAsExpected(wireCase, fromQueue);
        AssertRoutedAsExpected(wireCase, fromSendEndpoint);

        var queueFrame = _factory.EncodeForClients(fromQueue);
        var sendEndpointFrame = _factory.EncodeForClients(fromSendEndpoint);

        // Asserted first because it is the failure message a reader can act on.
        Assert.Equal(wireCase.ExpectedMessagePackJson, DecodeFrame(queueFrame));

        // The property this test exists for: ingress casing does not reach clients.
        Assert.Equal(Convert.ToHexString(queueFrame), Convert.ToHexString(sendEndpointFrame));

        // And the bytes are the pinned ones, down to the MessagePack type of every value.
        Assert.Equal(wireCase.ExpectedFrameHex, Convert.ToHexString(queueFrame));
    }

    private static void AssertRoutedAsExpected(WireCase wireCase, HubInvocation invocation)
    {
        Assert.Equal(wireCase.ExpectedHub, invocation.Hub);
        Assert.Equal(wireCase.ExpectedDestination, invocation.Destination);
        Assert.Equal(wireCase.ExpectedMethod, invocation.Method);
        Assert.Single(invocation.Arguments);
    }

    private async Task<HubInvocation> RouteThroughQueueAsync(string json)
    {
        await _queue.SendMessageAsync(json);

        // The hosted service dequeues on its own loop, so the send is only observable
        // asynchronously; the timeout guards against never observing it at all.
        using var cts = new CancellationTokenSource(_timeout);
        return await _queueRecorder.AwaitNextAsync(cts.Token);
    }

    private async Task<HubInvocation> RouteThroughSendEndpointAsync(string json)
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/send", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The endpoint routes to the hub before responding, so the notification is already
        // recorded by this point. The timeout only keeps a routing regression from hanging.
        using var cts = new CancellationTokenSource(_timeout);
        return await _factory.AwaitNextHubInvocationAsync(cts.Token);
    }

    // SignalR prefixes each MessagePack message with its length as a 7-bit encoded varint
    // (see BinaryMessageFormatter). Skip the prefix, then render the message itself so the
    // assertion failure shows a diffable shape instead of a wall of hex.
    private static string DecodeFrame(byte[] frame)
    {
        var offset = 0;
        while ((frame[offset] & 0x80) != 0)
        {
            offset++;
        }

        return MessagePackSerializer.ConvertToJson(frame.AsMemory(offset + 1));
    }
}
