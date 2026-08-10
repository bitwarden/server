using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Settings;
using Bit.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Notifications.Test;

/// <summary>
/// Integration tests for <c>POST /send</c> on the Notifications service.
///
/// The endpoint is the internal ingress for push notifications from other services.
/// Two contracts are enforced here:
/// <list type="number">
///   <item>Every format in <see cref="SupportedPayloads"/> must be accepted by the endpoint and
///         routed to the correct SignalR hub group.</item>
///   <item>Whatever <see cref="NotificationsApiPushEngine.PushAsync"/> currently produces must
///         be one of those formats, so any wire-format change is caught immediately.</item>
/// </list>
/// When <c>PushAsync</c> is updated to produce a new shape, add the new format to
/// <see cref="SupportedPayloads"/> and update <see cref="EngineInputArgs"/> if needed.
///
/// <para><strong>Not every push type is covered intentionally.</strong> The long-term goal is for
/// <c>POST /send</c> to be a dumb proxy: routing decisions should be driven entirely by
/// envelope-level fields (<c>Type</c>, <c>ContextId</c>, and a future target/clientType on the
/// envelope) rather than by inspecting the inner <c>Payload</c>. Once that migration is complete,
/// the payload becomes opaque to the endpoint and exhaustive per-type coverage here would add
/// noise without value. The representative sample in <see cref="SupportedPayloads"/> is
/// sufficient to guard the contract until then.</para>
/// </summary>
public sealed class PostSendEndpointTests : IAsyncDisposable
{
    // Fixed IDs used in all payload literals below — changing these requires updating SupportedPayloads.
    private static readonly Guid _userId = Guid.Parse("d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c");
    private static readonly Guid _orgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _installationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _notifId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const string TestContextId = "test-device-id";

    /// <summary>
    /// Every JSON format that <c>POST /send</c> must accept. When <c>PushAsync</c> changes its
    /// wire format, add the new shape here. Old shapes must be kept for at least one release to
    /// support rolling upgrades where the sender (e.g. Api) may still be on the previous version
    /// while the Notifications service has already been updated.
    ///
    /// <para><strong>Do not add a new entry here in the same commit that updates
    /// <c>POST /send</c> to handle it.</strong> A new entry proves the endpoint accepts the new
    /// format, but the point of keeping old entries is to prove the endpoint still accepts the
    /// <em>previous</em> format after it has been updated. If both changes land together the old
    /// entry is never tested against a Notifications build that lacks the new handling code, so
    /// you cannot tell from CI alone whether the deployment order matters.</para>
    /// </summary>
    private static readonly string[] SupportedPayloads =
    [
        // User — LogOut, no context exclusion
        """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":null}""",
        // User — LogOut, with context exclusion
        """{"Type":11,"Payload":{"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","Reason":null},"ContextId":"test-device-id"}""",
        // Organization — SyncOrganizationStatusChanged, no context exclusion
        """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":null}""",
        // Organization — SyncOrganizationStatusChanged, with context exclusion
        """{"Type":18,"Payload":{"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","Enabled":true},"ContextId":"test-device-id"}""",
        // Installation — Notification (ClientType.All), no context exclusion
        """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"ContextId":null}""",
        // Installation — Notification (ClientType.All), with context exclusion
        """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":0,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"ContextId":"test-device-id"}""",
        // User — Notification (ClientType.Mobile), routes to client-type-specific group
        """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":4,"UserId":"d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c","OrganizationId":null,"InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"ContextId":null}""",
        // Organization — Notification (ClientType.Mobile), routes to client-type-specific group
        """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":4,"UserId":null,"OrganizationId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","InstallationId":null,"TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"ContextId":null}""",
        // Installation — Notification (ClientType.Mobile), routes to client-type-specific group
        """{"Type":20,"Payload":{"Id":"cccccccc-cccc-cccc-cccc-cccccccccccc","Priority":0,"Global":false,"ClientType":4,"UserId":null,"OrganizationId":null,"InstallationId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","TaskId":null,"Title":null,"Body":null,"CreationDate":"0001-01-01T00:00:00","RevisionDate":"0001-01-01T00:00:00","ReadDate":null,"DeletedDate":null},"ContextId":null}""",
    ];

    // Each supported payload paired with the SignalR routing call it must trigger.
    // Real payload types are used here because HubHelpers inspects the inner payload to determine
    // which SignalR group to route to. Once that routing information moves into the envelope itself,
    // these can be replaced with a simple mock payload type.
    private sealed record RoutingCase(string Json, string? ExpectedUserId, string? ExpectedGroup);

    private static readonly RoutingCase[] RoutingCases =
    [
        new(SupportedPayloads[0], _userId.ToString(), null),
        new(SupportedPayloads[1], _userId.ToString(), null),
        new(SupportedPayloads[2], null, NotificationsHub.GetOrganizationGroup(_orgId)),
        new(SupportedPayloads[3], null, NotificationsHub.GetOrganizationGroup(_orgId)),
        new(SupportedPayloads[4], null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.All)),
        new(SupportedPayloads[5], null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.All)),
        new(SupportedPayloads[6], null, NotificationsHub.GetUserGroup(_userId, ClientType.Mobile)),
        new(SupportedPayloads[7], null, NotificationsHub.GetOrganizationGroup(_orgId, ClientType.Mobile)),
        new(SupportedPayloads[8], null, NotificationsHub.GetInstallationGroup(_installationId, ClientType.Mobile)),
    ];

    private readonly NotificationsApplicationFactory _factory = new();

    /// <summary>
    /// All (target, excludeCurrentContext) combinations exercised against the engine.
    /// </summary>
    public static IEnumerable<object[]> EngineInputArgs() =>
        from target in Enum.GetValues<NotificationTarget>()
        from excludeCurrentContext in new[] { false, true }
        select new object[] { target, excludeCurrentContext };

    /// <summary>
    /// All (target, clientType) combinations for the Notification push type. ClientType on the
    /// Notification payload controls which client-type-scoped SignalR group receives the message —
    /// the same filtering the Azure Notification Hub engine applies via tags on the mobile path.
    /// </summary>
    public static IEnumerable<object[]> NotificationClientTypeArgs() =>
        from target in Enum.GetValues<NotificationTarget>()
        select new object[] { target, ClientType.Mobile };

    public static IEnumerable<object?[]> RoutingCaseArgs() =>
        RoutingCases.Select(c => new object?[] { c.Json, c.ExpectedUserId, c.ExpectedGroup });

    /// <summary>
    /// Verifies that the JSON currently produced by <see cref="NotificationsApiPushEngine.PushAsync"/>
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
            $"NotificationsApiPushEngine.PushAsync produced a payload not listed in {nameof(SupportedPayloads)}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Supported:\n  {string.Join("\n  ", SupportedPayloads)}");
    }

    /// <summary>
    /// Verifies that every format in <see cref="SupportedPayloads"/> is accepted by
    /// <c>POST /send</c> and routed to the correct SignalR user or group.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoutingCaseArgs))]
    public async Task PostSend_RoutesPayloadToCorrectHubGroup(
        string json, string? expectedUserId, string? expectedGroup)
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/send", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        if (expectedUserId is not null)
        {
            _factory.NotificationsHubClients.Received(1).User(expectedUserId);
        }
        else
        {
            _factory.NotificationsHubClients.Received(1).Group(expectedGroup!);
        }
    }

    /// <summary>
    /// Verifies the full chain: a <see cref="NotificationsApiPushEngine.PushAsync"/> call with a
    /// <see cref="PushType.Notification"/> payload carrying a specific <see cref="ClientType"/>
    /// produces a wire format that the endpoint accepts and routes to the correct client-type-scoped
    /// SignalR group — matching the filtering the Azure Notification Hub engine applies via tags.
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
            $"Notification payload with {nameof(ClientType)}.{clientType} not listed in {nameof(SupportedPayloads)}.\n" +
            $"Captured:\n  {captured}\n" +
            $"Supported:\n  {string.Join("\n  ", SupportedPayloads)}");

        using var client = await _factory.CreateAuthenticatedClientAsync();
        using var content = new StringContent(captured, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/send", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var expectedGroup = target switch
        {
            NotificationTarget.User => NotificationsHub.GetUserGroup(_userId, clientType),
            NotificationTarget.Organization => NotificationsHub.GetOrganizationGroup(_orgId, clientType),
            NotificationTarget.Installation => NotificationsHub.GetInstallationGroup(_installationId, clientType),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        _factory.NotificationsHubClients.Received(1).Group(expectedGroup);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    // Runs a PushAsync invocation against mock HTTP handlers and returns the JSON body the engine
    // posted to /send, preserving the real JsonContent.Create serialization path.
    private static async Task<string> CapturePayloadAsync(
        bool excludeCurrentContext, Func<NotificationsApiPushEngine, Task> invoke)
    {
        const string notificationsBase = "http://localhost/";
        const string identityBase = "http://localhost/";

        var mockClient = new MockHttpMessageHandler();
        var mockIdentityClient = new MockHttpMessageHandler();

        using var notificationsClient = new HttpClient(mockClient);
        using var identityClient = new HttpClient(mockIdentityClient);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("client").Returns(notificationsClient);
        httpClientFactory.CreateClient("identity").Returns(identityClient);

        var globalSettings = new GlobalSettings
        {
            BaseServiceUri =
            {
                InternalNotifications = notificationsBase,
                InternalIdentity = identityBase,
            },
            InternalIdentityKey = "test-key",
            ProjectName = "test",
        };

        mockIdentityClient
            .Expect(HttpMethod.Post, $"{identityBase}connect/token")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { access_token = BuildTestToken() }));

        string? capturedJson = null;
        mockClient
            .Expect(HttpMethod.Post, $"{notificationsBase}send")
            .With(request =>
            {
                if (request.Content is JsonContent jsonContent)
                {
                    capturedJson = JsonSerializer.Serialize(jsonContent.Value);
                }
                return true;
            })
            .Respond(HttpStatusCode.OK);

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

        var engine = new NotificationsApiPushEngine(
            httpClientFactory,
            globalSettings,
            httpContextAccessor,
            NullLogger<NotificationsApiPushEngine>.Instance);

        await invoke(engine);

        return capturedJson ?? throw new InvalidOperationException("Engine did not POST to /send.");
    }

    private static Task PushForTargetAsync(
        NotificationsApiPushEngine engine, NotificationTarget target, bool excludeCurrentContext) =>
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

    // Builds a minimal unsigned JWT with a far-future exp so BaseIdentityClientService's
    // token-refresh check passes without requiring a real signing key.
    private static string BuildTestToken()
    {
        static string Encode(string json) =>
            Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

        return $"{Encode("""{"alg":"none","typ":"JWT"}""")}" +
               $".{Encode("""{"exp":9999999999}""")}.";
    }
}
