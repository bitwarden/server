using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Storage.Queues;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Settings;
using Bit.Notifications;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Notifications.Test;

/// <summary>
/// The wire contract for push notifications, from the engines that produce a payload through to the
/// MessagePack frame a connected client receives.
///
/// <para>Three tests over two lists. <see cref="PushScenarios"/> holds one entry per logical
/// notification: the <see cref="PushNotification{T}"/> to send, and the single destination and frame
/// it must produce. <see cref="WireCases"/> holds one entry per accepted payload format, tagged with
/// the ingress it arrives on. Many cases, one expected frame each — which is what makes ingress
/// conventions provably invisible to clients.</para>
///
/// <list type="number">
///   <item><see cref="AzureQueueEngine_ProducesSupportedPayload"/> — what
///         <see cref="AzureQueuePushEngine"/> currently writes must be a listed queue format.</item>
///   <item><see cref="SendEndpointEngine_ProducesSupportedPayload"/> — what
///         <see cref="NotificationsApiPushEngine"/> currently posts must be a listed endpoint format.</item>
///   <item><see cref="SupportedPayload_RoutesToExpectedDestinationAndFrame"/> — every listed format,
///         delivered through its real ingress, must route to the expected hub destination and
///         serialize to the expected frame.</item>
/// </list>
///
/// <para>Splitting production from delivery is what makes rolling upgrades testable. A format stays
/// listed after the engines stop producing it, so test 3 keeps proving the receiver still accepts
/// what an older sender may still be sending; only tests 1 and 2 track the current output. Add the
/// new format in one release, switch the engine in a later one.</para>
///
/// <para>The ingress formats disagree by convention — the queue writes PascalCase, <c>POST /send</c>
/// writes camelCase — and both are deserialized case-insensitively into the same CLR types by
/// <see cref="HubHelpers"/>. A single expected frame per case is therefore an assertion that ingress
/// conventions never reach clients.</para>
///
/// <para><strong>The frame assertions are stricter than what would actually break a client.</strong>
/// MessagePack maps are string-keyed, so clients decode by name: reordering properties, or adding one
/// clients ignore, changes these bytes without breaking any real consumer. The strictness makes every
/// change to the client-facing shape visible in review, but when a literal needs updating the question
/// to answer is whether clients can handle the new shape, not whether the bytes moved.</para>
/// </summary>
public sealed class PushNotificationWireFormatTests
    : IClassFixture<NotificationsApplicationFactory>, IAsyncLifetime
{
    private const string TestContextId = "test-device-id";

    private static readonly Guid _userId = Guid.Parse("d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c");
    private static readonly Guid _orgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _installationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _notificationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid _authRequestId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid _taskId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static readonly DateTime _wholeSecond = new(2026, 8, 20, 12, 34, 56, DateTimeKind.Utc);
    private static readonly DateTime _subSecond = _wholeSecond.AddTicks(1234567);
    private static readonly DateTime _nextDay = new(2026, 8, 21, 8, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>How a payload reaches <see cref="HubHelpers"/>.</summary>
    public enum Ingress
    {
        /// <summary>Written to the Azure Queue and dequeued by <see cref="AzureQueueHostedService"/>.</summary>
        AzureQueue,

        /// <summary>Posted to <c>POST /send</c> on the Notifications service.</summary>
        SendEndpoint,
    }

    /// <summary>
    /// One accepted payload format, and the ingress it arrives on.
    /// </summary>
    /// <param name="Scenario">The <see cref="PushScenario.Name"/> this payload represents.</param>
    /// <param name="Ingress">Which path delivers it.</param>
    /// <param name="Payload">The payload exactly as it arrives on the wire.</param>
    private sealed record WireCase(string Scenario, Ingress Ingress, string Payload);

    /// <summary>
    /// One logical notification: what to send, and the single destination and frame every payload
    /// format accepted for it must produce.
    /// </summary>
    /// <param name="Name">Identifies the scenario in test output and joins it to its cases.</param>
    /// <param name="Push">Sends this notification through whichever engine is under test.</param>
    /// <param name="ExpectedHub">Name of the hub type the notification must be routed through.</param>
    /// <param name="ExpectedDestination">The destination, as <c>User:{id}</c> or <c>Group:{name}</c>.</param>
    /// <param name="ExpectedMethod">The client method name, part of the wire contract.</param>
    /// <param name="ExpectedFrameJson">
    /// The decoded frame: <c>[messageType, headers, invocationId, target, [arguments], streamIds]</c>.
    /// Readable, but lossy: JSON has no timestamp type, so a <see cref="DateTime"/> encoded as a
    /// MessagePack extension renders identically to an ISO string. <paramref name="ExpectedFrameHex"/>
    /// is what pins the encoding.
    /// </param>
    /// <param name="ExpectedFrameHex">
    /// The exact frame, length prefix included. Unlike the decoded form this distinguishes every
    /// MessagePack type, so it catches a value that keeps its rendering while changing its encoding.
    /// </param>
    private sealed record PushScenario(
        string Name,
        Func<IPushEngine, Task> Push,
        string ExpectedHub,
        string ExpectedDestination,
        string ExpectedMethod,
        string ExpectedFrameJson,
        string ExpectedFrameHex);

    private static readonly PushScenario[] PushScenarios =
    [
        new(
            "LogOut/User",
            engine => engine.PushAsync(new PushNotification<LogOutPushNotification>
            {
                Type = PushType.LogOut,
                Target = NotificationTarget.User,
                TargetId = _userId,
                Payload = new LogOutPushNotification { UserId = _userId },
                ExcludeCurrentContext = false,
            }),
            nameof(NotificationsHub),
            $"User:{_userId}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":null}],[]]""",
            "65960180C0AE526563656976654D6573736167659183A4547970650BA75061796C6F616482A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763A6526561736F6EC0A9436F6E746578744964C090"),
        new(
            "LogOut/User/ExcludedContext",
            engine => engine.PushAsync(new PushNotification<LogOutPushNotification>
            {
                Type = PushType.LogOut,
                Target = NotificationTarget.User,
                TargetId = _userId,
                Payload = new LogOutPushNotification { UserId = _userId },
                ExcludeCurrentContext = true,
            }),
            nameof(NotificationsHub),
            $"User:{_userId}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":"test-device-id"}],[]]""",
            "73960180C0AE526563656976654D6573736167659183A4547970650BA75061796C6F616482A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763A6526561736F6EC0A9436F6E746578744964AE746573742D6465766963652D696490"),
        new(
            "OrganizationStatus/Organization",
            engine => engine.PushAsync(new PushNotification<OrganizationStatusPushNotification>
            {
                Type = PushType.SyncOrganizationStatusChanged,
                Target = NotificationTarget.Organization,
                TargetId = _orgId,
                Payload = new OrganizationStatusPushNotification { OrganizationId = _orgId, Enabled = true },
                ExcludeCurrentContext = false,
            }),
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetOrganizationGroup(_orgId)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":null}],[]]""",
            "6E960180C0AE526563656976654D6573736167659183A45479706512A75061796C6F616482AE4F7267616E697A6174696F6E4964D92461616161616161612D616161612D616161612D616161612D616161616161616161616161A7456E61626C6564C3A9436F6E746578744964C090"),
        new(
            "OrganizationStatus/Organization/ExcludedContext",
            engine => engine.PushAsync(new PushNotification<OrganizationStatusPushNotification>
            {
                Type = PushType.SyncOrganizationStatusChanged,
                Target = NotificationTarget.Organization,
                TargetId = _orgId,
                Payload = new OrganizationStatusPushNotification { OrganizationId = _orgId, Enabled = true },
                ExcludeCurrentContext = true,
            }),
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetOrganizationGroup(_orgId)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":"test-device-id"}],[]]""",
            "7C960180C0AE526563656976654D6573736167659183A45479706512A75061796C6F616482AE4F7267616E697A6174696F6E4964D92461616161616161612D616161612D616161612D616161612D616161616161616161616161A7456E61626C6564C3A9436F6E746578744964AE746573742D6465766963652D696490"),
        new(
            "Notification/Installation/AllClients",
            engine => engine.PushAsync(NotificationFor(NotificationTarget.Installation, ClientType.All)),
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetInstallationGroup(_installationId, ClientType.All)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00.0000000Z","RevisionDate":"0001-01-01T00:00:00.0000000Z","ReadDate":null,"DeletedDate":null},"ContextId":null}],[]]""",
            "A802960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747900A6476C6F62616CC2AA436C69656E745479706500A6557365724964C0AE4F7267616E697A6174696F6E4964C0AE496E7374616C6C6174696F6E4964D92462626262626262622D626262622D626262622D626262622D626262626262626262626262A65461736B4964C0A55469746C65C0A4426F6479C0AC4372656174696F6E44617465C70CFF00000000FFFFFFF1886E0900AC5265766973696F6E44617465C70CFF00000000FFFFFFF1886E0900A85265616444617465C0AB44656C6574656444617465C0A9436F6E746578744964C090"),
        new(
            "Notification/User/Browser",
            engine => engine.PushAsync(NotificationFor(NotificationTarget.User, ClientType.Browser)),
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetUserGroup(_userId, ClientType.Browser)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00.0000000Z","RevisionDate":"0001-01-01T00:00:00.0000000Z","ReadDate":null,"DeletedDate":null},"ContextId":null}],[]]""",
            "A802960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747900A6476C6F62616CC2AA436C69656E745479706502A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763AE4F7267616E697A6174696F6E4964C0AE496E7374616C6C6174696F6E4964C0A65461736B4964C0A55469746C65C0A4426F6479C0AC4372656174696F6E44617465C70CFF00000000FFFFFFF1886E0900AC5265766973696F6E44617465C70CFF00000000FFFFFFF1886E0900A85265616444617465C0AB44656C6574656444617465C0A9436F6E746578744964C090"),
        new(
            "Notification/Organization/Browser",
            engine => engine.PushAsync(NotificationFor(NotificationTarget.Organization, ClientType.Browser)),
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetOrganizationGroup(_orgId, ClientType.Browser)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":null,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00.0000000Z","RevisionDate":"0001-01-01T00:00:00.0000000Z","ReadDate":null,"DeletedDate":null},"ContextId":null}],[]]""",
            "A802960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747900A6476C6F62616CC2AA436C69656E745479706502A6557365724964C0AE4F7267616E697A6174696F6E4964D92461616161616161612D616161612D616161612D616161612D616161616161616161616161AE496E7374616C6C6174696F6E4964C0A65461736B4964C0A55469746C65C0A4426F6479C0AC4372656174696F6E44617465C70CFF00000000FFFFFFF1886E0900AC5265766973696F6E44617465C70CFF00000000FFFFFFF1886E0900A85265616444617465C0AB44656C6574656444617465C0A9436F6E746578744964C090"),
        new(
            "Notification/Installation/Browser",
            engine => engine.PushAsync(NotificationFor(NotificationTarget.Installation, ClientType.Browser)),
            nameof(NotificationsHub),
            $"Group:{NotificationsHub.GetInstallationGroup(_installationId, ClientType.Browser)}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00.0000000Z","RevisionDate":"0001-01-01T00:00:00.0000000Z","ReadDate":null,"DeletedDate":null},"ContextId":null}],[]]""",
            "A802960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747900A6476C6F62616CC2AA436C69656E745479706502A6557365724964C0AE4F7267616E697A6174696F6E4964C0AE496E7374616C6C6174696F6E4964D92462626262626262622D626262622D626262622D626262622D626262626262626262626262A65461736B4964C0A55469746C65C0A4426F6479C0AC4372656174696F6E44617465C70CFF00000000FFFFFFF1886E0900AC5265766973696F6E44617465C70CFF00000000FFFFFFF1886E0900A85265616444617465C0AB44656C6574656444617465C0A9436F6E746578744964C090"),
        new(
            "AuthRequestResponse/AnonymousHub",
            engine => engine.PushAsync(new PushNotification<AuthRequestPushNotification>
            {
                Type = PushType.AuthRequestResponse,
                Target = NotificationTarget.User,
                TargetId = _userId,
                Payload = new AuthRequestPushNotification { UserId = _userId, Id = _authRequestId },
                ExcludeCurrentContext = true,
            }),
            nameof(AnonymousNotificationsHub),
            $"Group:{_authRequestId}",
            "AuthRequestResponseRecieved",
            """[1,{},null,"AuthRequestResponseRecieved",[{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id"}],[]]""",
            "A101960180C0BB4175746852657175657374526573706F6E736552656369657665649183A45479706510A75061796C6F616482A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763A24964D92464646464646464642D646464642D646464642D646464642D646464646464646464646464A9436F6E746578744964AE746573742D6465766963652D696490"),
        // Every other case leaves the dates at DateTime.MinValue, which encodes as the one timestamp
        // format production never produces. This case carries values that reach the other two, along
        // with a populated nullable date and non-null strings.
        new(
            "Notification/User/RealisticValues",
            engine => engine.PushAsync(new PushNotification<NotificationPushNotification>
            {
                Type = PushType.Notification,
                Target = NotificationTarget.User,
                TargetId = _userId,
                Payload = new NotificationPushNotification
                {
                    Id = _notificationId,
                    Priority = Priority.High,
                    ClientType = ClientType.All,
                    UserId = _userId,
                    TaskId = _taskId,
                    Title = "Test title",
                    Body = "Test body",
                    CreationDate = _wholeSecond,
                    RevisionDate = _subSecond,
                    ReadDate = _nextDay,
                },
                ClientType = ClientType.All,
                ExcludeCurrentContext = false,
            }),
            nameof(NotificationsHub),
            $"User:{_userId}",
            "ReceiveMessage",
            """[1,{},null,"ReceiveMessage",[{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":3,"Global":false,"ClientType":0,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","Title":"Test title","Body":"Test body","CreationDate":"2026-08-20T12:34:56.0000000Z","RevisionDate":"2026-08-20T12:34:56.1234567Z","ReadDate":"2026-08-21T08:00:00.0000000Z","DeletedDate":null},"ContextId":null}],[]]""",
            "D702960180C0AE526563656976654D6573736167659183A45479706514A75061796C6F61648EA24964D92463636363636363632D636363632D636363632D636363632D636363636363636363636363A85072696F7269747903A6476C6F62616CC2AA436C69656E745479706500A6557365724964D92464326561356237322D366434372D346432302D623561332D623761366538396438653763AE4F7267616E697A6174696F6E4964C0AE496E7374616C6C6174696F6E4964C0A65461736B4964D92465656565656565652D656565652D656565652D656565652D656565656565656565656565A55469746C65AA54657374207469746C65A4426F6479A95465737420626F6479AC4372656174696F6E44617465D6FF6A86F470AC5265766973696F6E44617465D7FF1D6F32F06A86F470A85265616444617465D6FF6A880580AB44656C6574656444617465C0A9436F6E746578744964C090"),
    ];

    /// <summary>
    /// Every payload format the service accepts, one entry each, in two generations: what the engines
    /// write now, and what senders deployed before this release write. Each section carries the note
    /// explaining its status.
    ///
    /// <para>A format stays listed after the engine stops producing it, because a sender that has
    /// not been redeployed can still be sending it. That is what lets the sender change ship in one
    /// release and the superseded format be deleted in a later one.</para>
    /// </summary>
    private static readonly WireCase[] WireCases =
    [
        // What both engines write now, all of it landing in one release: PascalCase, envelope
        // routing fields, and nulls omitted from the envelope while the payload states its own.
        // The routing fields are stripped before a notification reaches clients, which is why
        // every frame below is unchanged from the generation that predates them.
        new("LogOut/User", Ingress.AzureQueue, """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}"""),
        new("LogOut/User/ExcludedContext", Ingress.AzureQueue, """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":"test-device-id","Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}"""),
        new("OrganizationStatus/Organization", Ingress.AzureQueue, """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"Target":1,"TargetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
        new("OrganizationStatus/Organization/ExcludedContext", Ingress.AzureQueue, """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":"test-device-id","Target":1,"TargetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
        new("Notification/Installation/AllClients", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":2,"TargetId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","ClientType":0}"""),
        new("Notification/User/Browser", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","ClientType":2}"""),
        new("Notification/Organization/Browser", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":null,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":1,"TargetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","ClientType":2}"""),
        new("Notification/Installation/Browser", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":2,"TargetId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","ClientType":2}"""),
        new("AuthRequestResponse/AnonymousHub", Ingress.AzureQueue, """{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id","Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}"""),
        new("Notification/User/RealisticValues", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":3,"Global":false,"ClientType":0,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","Title":"Test title","Body":"Test body","CreationDate":"2026-08-20T12:34:56Z","RevisionDate":"2026-08-20T12:34:56.1234567Z","ReadDate":"2026-08-21T08:00:00Z","DeletedDate":null},"Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","ClientType":0}"""),
        new("LogOut/User", Ingress.SendEndpoint, """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}"""),
        new("LogOut/User/ExcludedContext", Ingress.SendEndpoint, """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":"test-device-id","Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}"""),
        new("OrganizationStatus/Organization", Ingress.SendEndpoint, """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"Target":1,"TargetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
        new("OrganizationStatus/Organization/ExcludedContext", Ingress.SendEndpoint, """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":"test-device-id","Target":1,"TargetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
        new("Notification/Installation/AllClients", Ingress.SendEndpoint, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":2,"TargetId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","ClientType":0}"""),
        new("Notification/User/Browser", Ingress.SendEndpoint, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","ClientType":2}"""),
        new("Notification/Organization/Browser", Ingress.SendEndpoint, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":null,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":1,"TargetId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","ClientType":2}"""),
        new("Notification/Installation/Browser", Ingress.SendEndpoint, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"Target":2,"TargetId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","ClientType":2}"""),
        new("AuthRequestResponse/AnonymousHub", Ingress.SendEndpoint, """{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id","Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}"""),
        new("Notification/User/RealisticValues", Ingress.SendEndpoint, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":3,"Global":false,"ClientType":0,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","Title":"Test title","Body":"Test body","CreationDate":"2026-08-20T12:34:56Z","RevisionDate":"2026-08-20T12:34:56.1234567Z","ReadDate":"2026-08-21T08:00:00Z","DeletedDate":null},"Target":0,"TargetId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","ClientType":0}"""),

        // What senders deployed before that release write, and so what this service must keep
        // accepting until none of them are left: PascalCase with null-valued properties omitted
        // on the queue, camelCase on the endpoint, and no envelope routing on either -- these
        // route by payload inspection. Delete this section one release after the one above
        // ships, along with the payload-derived fallback in HubHelpers.
        new("LogOut/User", Ingress.AzureQueue, """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"}}"""),
        new("LogOut/User/ExcludedContext", Ingress.AzureQueue, """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c"},"ContextId":"test-device-id"}"""),
        new("OrganizationStatus/Organization", Ingress.AzureQueue, """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true}}"""),
        new("OrganizationStatus/Organization/ExcludedContext", Ingress.AzureQueue, """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":"test-device-id"}"""),
        new("Notification/Installation/AllClients", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}"""),
        new("Notification/User/Browser", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}"""),
        new("Notification/Organization/Browser", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}"""),
        new("Notification/Installation/Browser", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":2,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}"""),
        new("AuthRequestResponse/AnonymousHub", Ingress.AzureQueue, """{"Type":16,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"ContextId":"test-device-id"}"""),
        new("Notification/User/RealisticValues", Ingress.AzureQueue, """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":3,"Global":false,"ClientType":0,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","TaskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","Title":"Test title","Body":"Test body","CreationDate":"2026-08-20T12:34:56Z","RevisionDate":"2026-08-20T12:34:56.1234567Z","ReadDate":"2026-08-21T08:00:00Z"}}"""),

        new("LogOut/User", Ingress.SendEndpoint, """{"type":11,"payload":{"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","reason":null},"contextId":null}"""),
        new("LogOut/User/ExcludedContext", Ingress.SendEndpoint, """{"type":11,"payload":{"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","reason":null},"contextId":"test-device-id"}"""),
        new("OrganizationStatus/Organization", Ingress.SendEndpoint, """{"type":18,"payload":{"organizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","enabled":true},"contextId":null}"""),
        new("OrganizationStatus/Organization/ExcludedContext", Ingress.SendEndpoint, """{"type":18,"payload":{"organizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","enabled":true},"contextId":"test-device-id"}"""),
        new("Notification/Installation/AllClients", Ingress.SendEndpoint, """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":0,"global":false,"clientType":0,"userId":null,"organizationId":null,"installationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","taskId":null,"title":null,"body":null,"creationDate":"0001-01-01T00:00:00","revisionDate":"0001-01-01T00:00:00","readDate":null,"deletedDate":null},"contextId":null}"""),
        new("Notification/User/Browser", Ingress.SendEndpoint, """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":0,"global":false,"clientType":2,"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","organizationId":null,"installationId":null,"taskId":null,"title":null,"body":null,"creationDate":"0001-01-01T00:00:00","revisionDate":"0001-01-01T00:00:00","readDate":null,"deletedDate":null},"contextId":null}"""),
        new("Notification/Organization/Browser", Ingress.SendEndpoint, """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":0,"global":false,"clientType":2,"userId":null,"organizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","installationId":null,"taskId":null,"title":null,"body":null,"creationDate":"0001-01-01T00:00:00","revisionDate":"0001-01-01T00:00:00","readDate":null,"deletedDate":null},"contextId":null}"""),
        new("Notification/Installation/Browser", Ingress.SendEndpoint, """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":0,"global":false,"clientType":2,"userId":null,"organizationId":null,"installationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","taskId":null,"title":null,"body":null,"creationDate":"0001-01-01T00:00:00","revisionDate":"0001-01-01T00:00:00","readDate":null,"deletedDate":null},"contextId":null}"""),
        new("AuthRequestResponse/AnonymousHub", Ingress.SendEndpoint, """{"type":16,"payload":{"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","id":"dddddddd-dddd-dddd-dddd-dddddddddddd"},"contextId":"test-device-id"}"""),
        new("Notification/User/RealisticValues", Ingress.SendEndpoint, """{"type":20,"payload":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","priority":3,"global":false,"clientType":0,"userId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","organizationId":null,"installationId":null,"taskId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","title":"Test title","body":"Test body","creationDate":"2026-08-20T12:34:56Z","revisionDate":"2026-08-20T12:34:56.1234567Z","readDate":"2026-08-21T08:00:00Z","deletedDate":null},"contextId":null}"""),
    ];

    private readonly NotificationsApplicationFactory _factory;
    private readonly ChannelQueueClient _queue = new();
    private readonly HubInvocationRecorder _queueRecorder = new();
    private readonly IHost _queueHost;

    public PushNotificationWireFormatTests(NotificationsApplicationFactory factory)
    {
        _factory = factory;

        // The queue consumer cannot run inside the factory's app: it bails out when SelfHosted is
        // true, which is exactly when SendController is enabled. So the queue ingress gets its own
        // host running the real hosted service, while the endpoint ingress uses the real app.
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
                // So a test can assert on what dequeuing logged.
                services.AddFakeLogging();
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
    }

    public static IEnumerable<object[]> ScenarioNames() =>
        PushScenarios.Select(scenario => new object[] { scenario.Name });

    // The index identifies the case; the scenario and ingress ride along so failures name themselves.
    public static IEnumerable<object[]> QueueCaseArgs() =>
        WireCases.Select((wireCase, index) => (wireCase, index))
            .Where(pair => pair.wireCase.Ingress == Ingress.AzureQueue)
            .Select(pair => new object[] { pair.wireCase.Scenario, pair.index });

    public static IEnumerable<object[]> WireCaseArgs() =>
        WireCases.Select((wireCase, index) => new object[] { wireCase.Scenario, wireCase.Ingress, index });

    /// <summary>
    /// Verifies that the payload <see cref="AzureQueuePushEngine"/> currently writes for each
    /// notification is one of the formats listed for it. Fails when the engine changes its wire
    /// format without the new format being listed first.
    /// </summary>
    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task AzureQueueEngine_ProducesSupportedPayload(string name)
    {
        var scenario = GetScenario(name);
        var captured = await CaptureQueuePayloadAsync(scenario);

        AssertIsSupported(captured, scenario, Ingress.AzureQueue, nameof(AzureQueuePushEngine));
    }

    /// <summary>
    /// Verifies that the payload <see cref="NotificationsApiPushEngine"/> currently posts to
    /// <c>POST /send</c> for each notification is one of the formats listed for it.
    /// </summary>
    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task SendEndpointEngine_ProducesSupportedPayload(string name)
    {
        var scenario = GetScenario(name);
        var captured = await CaptureSendPayloadAsync(scenario);

        AssertIsSupported(captured, scenario, Ingress.SendEndpoint, nameof(NotificationsApiPushEngine));
    }

    /// <summary>
    /// Delivers one accepted payload through its real ingress and verifies where it was routed and
    /// the exact bytes clients would receive. Every case of a scenario — either ingress, current or
    /// superseded — is checked against that scenario's single expected destination and frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(WireCaseArgs))]
    public async Task SupportedPayload_RoutesToExpectedDestinationAndFrame(string scenario, Ingress ingress, int index)
    {
        var wireCase = WireCases[index];
        Assert.Equal((scenario, ingress), (wireCase.Scenario, wireCase.Ingress));

        var expected = GetScenario(wireCase.Scenario);
        var invocation = await DeliverAsync(wireCase);

        Assert.Equal(expected.ExpectedHub, invocation.Hub);
        Assert.Equal(expected.ExpectedDestination, invocation.Destination);
        Assert.Equal(expected.ExpectedMethod, invocation.Method);
        Assert.Single(invocation.Arguments);

        var frame = _factory.EncodeForClients(invocation);

        // Asserted first because it is the failure message a reader can act on.
        Assert.Equal(expected.ExpectedFrameJson, DecodeFrame(frame));

        // And the bytes are the pinned ones, down to the MessagePack type of every value.
        Assert.Equal(expected.ExpectedFrameHex, Convert.ToHexString(frame));
    }

    /// <summary>
    /// A queued message may be base64-encoded rather than plain text, and the service accepts either.
    /// Pinned before anything reads the message as bytes, because <c>DecodeMessageText</c> is what
    /// provides the tolerance today: deserializing the body directly would accept only plain text, and
    /// this is the test that would notice.
    /// </summary>
    [Theory]
    [MemberData(nameof(QueueCaseArgs))]
    public async Task Base64EncodedQueueMessage_RoutesLikePlainText(string scenario, int index)
    {
        var wireCase = WireCases[index];
        var expected = GetScenario(scenario);

        await _queue.SendMessageAsync(
            BinaryData.FromBytes(Encoding.UTF8.GetBytes(Convert.ToBase64String(
                Encoding.UTF8.GetBytes(wireCase.Payload)))));

        using var cts = new CancellationTokenSource(_timeout);
        var invocation = await _queueRecorder.AwaitNextAsync(cts.Token);

        Assert.Equal(expected.ExpectedHub, invocation.Hub);
        Assert.Equal(expected.ExpectedDestination, invocation.Destination);
        Assert.Equal(expected.ExpectedFrameHex, Convert.ToHexString(_factory.EncodeForClients(invocation)));
    }

    /// <summary>
    /// Dequeuing says so when it had to base64-decode a message. Nothing writes base64 any more, so
    /// this is how we find out whether the tolerance is still load-bearing before removing it -- which
    /// only works if the log actually fires, hence this test and the one below it.
    /// </summary>
    [Fact]
    public async Task Base64EncodedQueueMessage_IsLogged()
    {
        var wireCase = WireCases.First(c => c.Ingress == Ingress.AzureQueue);

        await _queue.SendMessageAsync(BinaryData.FromString(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(wireCase.Payload))));

        using var cts = new CancellationTokenSource(_timeout);
        await _queueRecorder.AwaitNextAsync(cts.Token);

        Assert.Contains(
            _queueHost.Services.GetRequiredService<FakeLogCollector>().GetSnapshot(),
            record => record.Level == LogLevel.Warning && record.Message.Contains("base64-encoded"));
    }

    /// <summary>
    /// The counterpart: a plain message says nothing, so the warning means what it says rather than
    /// firing on everything.
    /// </summary>
    [Fact]
    public async Task PlainTextQueueMessage_IsNotLoggedAsBase64()
    {
        var wireCase = WireCases.First(c => c.Ingress == Ingress.AzureQueue);

        await _queue.SendMessageAsync(wireCase.Payload);

        using var cts = new CancellationTokenSource(_timeout);
        await _queueRecorder.AwaitNextAsync(cts.Token);

        Assert.DoesNotContain(
            _queueHost.Services.GetRequiredService<FakeLogCollector>().GetSnapshot(),
            record => record.Message.Contains("base64-encoded"));
    }

    /// <summary>
    /// The queue carries bytes, and a test can now queue them directly. Nothing reads a message that
    /// way yet; this proves the fake supports it, so the reader can change without the harness having
    /// to change with it.
    /// </summary>
    [Fact]
    public async Task QueueMessageQueuedAsBytes_RoutesLikeAString()
    {
        var wireCase = WireCases.First(c => c.Ingress == Ingress.AzureQueue);
        var expected = GetScenario(wireCase.Scenario);

        await _queue.SendMessageAsync(BinaryData.FromBytes(Encoding.UTF8.GetBytes(wireCase.Payload)));

        using var cts = new CancellationTokenSource(_timeout);
        var invocation = await _queueRecorder.AwaitNextAsync(cts.Token);

        Assert.Equal(expected.ExpectedDestination, invocation.Destination);
        Assert.Equal(expected.ExpectedFrameHex, Convert.ToHexString(_factory.EncodeForClients(invocation)));
    }

    private static PushScenario GetScenario(string name) => PushScenarios.Single(s => s.Name == name);

    private static void AssertIsSupported(
        string captured, PushScenario scenario, Ingress ingress, string engine)
    {
        var accepted = WireCases
            .Where(c => c.Scenario == scenario.Name && c.Ingress == ingress)
            .Select(c => c.Payload)
            .ToArray();

        var capturedNode = JsonNode.Parse(captured);
        Assert.True(
            accepted.Any(payload => JsonNode.DeepEquals(capturedNode, JsonNode.Parse(payload))),
            $"{engine} produced a payload not listed for {scenario.Name}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Listed:\n  {string.Join("\n  ", accepted)}");
    }

    private static PushNotification<NotificationPushNotification> NotificationFor(
        NotificationTarget target, ClientType clientType) =>
        new()
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
                Id = _notificationId,
                ClientType = clientType,
                UserId = target == NotificationTarget.User ? _userId : null,
                OrganizationId = target == NotificationTarget.Organization ? _orgId : null,
                InstallationId = target == NotificationTarget.Installation ? _installationId : null,
            },
            // Set on the envelope as well as the payload, matching what the notification centre
            // push sites do -- envelope routing reads it from here.
            ClientType = clientType,
            ExcludeCurrentContext = false,
        };

    private async Task<HubInvocation> DeliverAsync(WireCase wireCase)
    {
        using var cts = new CancellationTokenSource(_timeout);

        if (wireCase.Ingress == Ingress.AzureQueue)
        {
            await _queue.SendMessageAsync(wireCase.Payload);
            return await _queueRecorder.AwaitNextAsync(cts.Token);
        }

        // The factory is shared across this class, so clear anything a previous case left behind.
        _factory.DiscardRecordedHubInvocations();

        using var client = await _factory.CreateAuthenticatedClientAsync();
        using var content = new StringContent(wireCase.Payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/send", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await _factory.AwaitNextHubInvocationAsync(cts.Token);
    }

    // Runs the notification through AzureQueuePushEngine and returns what it wrote to the queue.
    private static async Task<string> CaptureQueuePayloadAsync(PushScenario scenario)
    {
        var captureQueue = new ChannelQueueClient();
        var engine = new AzureQueuePushEngine(
            captureQueue,
            CurrentContextAccessor(),
            new GlobalSettings { Installation = { Id = _installationId } },
            NullLogger<AzureQueuePushEngine>.Instance);

        await scenario.Push(engine);

        using var cts = new CancellationTokenSource(_timeout);
        return await captureQueue.AwaitNextSentAsync(cts.Token);
    }

    // Runs the notification through NotificationsApiPushEngine against mock HTTP handlers and
    // returns the body it posted, preserving the real serialization path.
    private static async Task<string> CaptureSendPayloadAsync(PushScenario scenario)
    {
        const string baseUri = "http://localhost/";

        var mockNotifications = new MockHttpMessageHandler();
        var mockIdentity = new MockHttpMessageHandler();

        using var notificationsClient = new HttpClient(mockNotifications);
        using var identityClient = new HttpClient(mockIdentity);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("client").Returns(notificationsClient);
        httpClientFactory.CreateClient("identity").Returns(identityClient);

        mockIdentity
            .Expect(HttpMethod.Post, $"{baseUri}connect/token")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { access_token = BuildTestToken() }));

        string? capturedJson = null;
        mockNotifications
            .Expect(HttpMethod.Post, $"{baseUri}send")
            .With(request =>
            {
                if (request.Content is not null)
                {
                    capturedJson = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                return true;
            })
            .Respond(HttpStatusCode.OK);

        var engine = new NotificationsApiPushEngine(
            httpClientFactory,
            new GlobalSettings
            {
                BaseServiceUri = { InternalNotifications = baseUri, InternalIdentity = baseUri },
                InternalIdentityKey = "test-key",
                ProjectName = "test",
            },
            CurrentContextAccessor(),
            NullLogger<NotificationsApiPushEngine>.Instance);

        await scenario.Push(engine);

        return capturedJson ?? throw new InvalidOperationException("Engine did not post to /send.");
    }

    // Both engines read the current device identifier only when a notification excludes its own
    // context, so this can be wired unconditionally.
    private static IHttpContextAccessor CurrentContextAccessor()
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.DeviceIdentifier.Returns(TestContextId);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ICurrentContext)).Returns(currentContext);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.RequestServices.Returns(serviceProvider);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
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

    // A minimal unsigned JWT with a far-future exp, so BaseIdentityClientService's token refresh
    // check passes without a real signing key.
    private static string BuildTestToken()
    {
        static string Encode(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

        return $"{Encode("""{"alg":"none","typ":"JWT"}""")}.{Encode("""{"exp":9999999999}""")}.";
    }
}
