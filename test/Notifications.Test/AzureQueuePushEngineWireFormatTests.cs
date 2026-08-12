#nullable enable
using System.Text.Json.Nodes;
using Azure.Storage.Queues;
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
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Notifications.Test;

/// <summary>
/// Wire format guard for <see cref="AzureQueuePushEngine"/>.
///
/// The Azure Queue path flows: <see cref="AzureQueuePushEngine"/> → Azure Queue →
/// <see cref="AzureQueueHostedService"/> → <see cref="HubHelpers.SendNotificationToHubAsync"/>.
/// Two contracts are enforced here:
/// <list type="number">
///   <item>Every format in <see cref="SupportedPayloads"/> must be routed correctly by
///         <see cref="HubHelpers"/>, which peeks into the payload to determine the SignalR
///         group. Once that routing information moves into the envelope, these can be simplified.</item>
///   <item>Whatever <see cref="AzureQueuePushEngine.PushAsync"/> currently produces must be one
///         of those formats, so any wire-format change is caught immediately.</item>
/// </list>
///
/// <para>The logical routing cases mirror <see cref="PostSendEndpointTests"/>. The JSON differs
/// because <see cref="AzureQueuePushEngine"/> serializes with <c>JsonHelpers.IgnoreWritingNull</c>
/// (PascalCase, null values omitted) while <see cref="NotificationsApiPushEngine"/> uses
/// <c>JsonContent.Create</c> (camelCase, null values present). <see cref="HubHelpers"/> handles
/// both because its deserializer uses <c>PropertyNameCaseInsensitive = true</c>.</para>
/// </summary>
public sealed class AzureQueuePushEngineWireFormatTests
{
    // Fixed IDs used in all payload literals — changing these requires updating SupportedPayloads.
    // Kept in sync with PostSendEndpointTests for parity.
    private static readonly Guid _userId = Guid.Parse("d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c");
    private static readonly Guid _orgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _installationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _notifId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const string TestContextId = "test-device-id";

    private sealed record RoutingCase(string Json, string? ExpectedUserId, string? ExpectedGroup);

    /// <summary>
    /// Every wire format that <see cref="AzureQueuePushEngine"/> produces, paired with the
    /// SignalR routing call it must trigger in <see cref="HubHelpers"/>.
    ///
    /// <para>Serialized with <c>JsonHelpers.IgnoreWritingNull</c>: PascalCase property names,
    /// null values omitted. These are the structural equivalents of the entries in
    /// <see cref="PostSendEndpointTests"/>.</para>
    /// </summary>
    private static readonly RoutingCase[] RoutingCases =
    [
        // User — LogOut, no context exclusion
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
        // User — Notification (ClientType.Mobile), routes to client-type-specific group
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":4,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetUserGroup(_userId, ClientType.Mobile)),
        // Organization — Notification (ClientType.Mobile), routes to client-type-specific group
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":4,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetOrganizationGroup(_orgId, ClientType.Mobile)),
        // Installation — Notification (ClientType.Mobile), routes to client-type-specific group
        new("""{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":4,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00"}}""",
            null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.Mobile)),
    ];

    private static readonly string[] SupportedPayloads = RoutingCases.Select(c => c.Json).ToArray();

    /// <summary>All (target, excludeCurrentContext) combinations exercised against the engine.</summary>
    public static IEnumerable<object[]> EngineInputArgs() =>
        from target in Enum.GetValues<NotificationTarget>()
        from excludeCurrentContext in new[] { false, true }
        select new object[] { target, excludeCurrentContext };

    /// <summary>All (target, clientType) combinations for the Notification push type.</summary>
    public static IEnumerable<object[]> NotificationClientTypeArgs() =>
        from target in Enum.GetValues<NotificationTarget>()
        select new object[] { target, ClientType.Mobile };

    public static IEnumerable<object?[]> RoutingCaseArgs() =>
        RoutingCases.Select(c => new object?[] { c.Json, c.ExpectedUserId, c.ExpectedGroup });

    /// <summary>
    /// Verifies that the JSON currently produced by <see cref="AzureQueuePushEngine.PushAsync"/>
    /// for every (target, context) combination is represented in <see cref="SupportedPayloads"/>.
    /// Fails when <c>PushAsync</c> changes its wire format without a corresponding update.
    /// </summary>
    [Theory]
    [MemberData(nameof(EngineInputArgs))]
    public async Task PushAsync_ProducesASupportedPayload(NotificationTarget target, bool excludeCurrentContext)
    {
        var queueClient = Substitute.For<QueueClient>();
        var engine = BuildEngine(queueClient, excludeCurrentContext);

        await PushForTargetAsync(engine, target, excludeCurrentContext);

        var captured = CaptureQueueMessage(queueClient);
        var capturedNode = JsonNode.Parse(captured);

        Assert.True(
            SupportedPayloads.Any(s => JsonNode.DeepEquals(capturedNode, JsonNode.Parse(s))),
            $"AzureQueuePushEngine.PushAsync produced a payload not listed in {nameof(SupportedPayloads)}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Supported:\n  {string.Join("\n  ", SupportedPayloads)}");
    }

    /// <summary>
    /// Verifies the Notification payload with a specific ClientType produces a supported wire
    /// format — the same filtering that the Azure Notification Hub engine applies via tags.
    /// </summary>
    [Theory]
    [MemberData(nameof(NotificationClientTypeArgs))]
    public async Task PushAsync_Notification_ProducesASupportedPayload(NotificationTarget target, ClientType clientType)
    {
        var queueClient = Substitute.For<QueueClient>();
        var engine = BuildEngine(queueClient);

        await engine.PushAsync(new PushNotification<NotificationPushNotification>
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
        });

        var captured = CaptureQueueMessage(queueClient);
        Assert.True(
            SupportedPayloads.Any(s => JsonNode.DeepEquals(JsonNode.Parse(captured), JsonNode.Parse(s))),
            $"Notification payload with {nameof(ClientType)}.{clientType} not listed in {nameof(SupportedPayloads)}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Supported:\n  {string.Join("\n  ", SupportedPayloads)}");
    }

    /// <summary>
    /// Verifies that every format in <see cref="SupportedPayloads"/> is routed by
    /// <see cref="HubHelpers"/> to the correct SignalR user or group by peeking into the payload.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoutingCaseArgs))]
    public async Task HubHelpers_RoutesPayloadToCorrectHubGroup(
        string json, string? expectedUserId, string? expectedGroup)
    {
        var (hubHelpers, hubClients) = BuildHubHelpers();

        await hubHelpers.SendNotificationToHubAsync(json);

        if (expectedUserId is not null)
        {
            hubClients.Received(1).User(expectedUserId);
        }
        else
        {
            hubClients.Received(1).Group(expectedGroup!);
        }
    }

    private static AzureQueuePushEngine BuildEngine(QueueClient queueClient, bool excludeCurrentContext = false)
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        if (excludeCurrentContext)
        {
            var currentContext = Substitute.For<ICurrentContext>();
            currentContext.DeviceIdentifier = TestContextId;
            var httpContext = new DefaultHttpContext();
            var services = new ServiceCollection();
            services.AddSingleton(currentContext);
            httpContext.RequestServices = services.BuildServiceProvider();
            httpContextAccessor.HttpContext.Returns(httpContext);
        }

        return new AzureQueuePushEngine(
            queueClient,
            httpContextAccessor,
            new GlobalSettings(),
            NullLogger<AzureQueuePushEngine>.Instance);
    }

    private static (HubHelpers HubHelpers, IHubClients HubClients) BuildHubHelpers()
    {
        var proxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.User(Arg.Any<string>()).Returns(proxy);
        clients.Group(Arg.Any<string>()).Returns(proxy);

        var hubContext = Substitute.For<IHubContext<NotificationsHub>>();
        hubContext.Clients.Returns(clients);

        var anonymousHubContext = Substitute.For<IHubContext<AnonymousNotificationsHub>>();
        var hubHelpers = new HubHelpers(hubContext, anonymousHubContext, NullLogger<HubHelpers>.Instance);
        return (hubHelpers, clients);
    }

    private static string CaptureQueueMessage(QueueClient queueClient)
    {
        var call = queueClient.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(QueueClient.SendMessageAsync));
        return (string)call.GetArguments()[0]!;
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
