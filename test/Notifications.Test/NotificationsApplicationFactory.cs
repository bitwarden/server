using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Notifications.Test;

/// <summary>
/// Wraps a <see cref="WebApplicationFactory{TEntryPoint}"/> for the Notifications service alongside
/// an in-memory Identity server that issues real JWT tokens. Tests interact with the service through
/// <see cref="HttpClient"/> only.
/// </summary>
public sealed class NotificationsApplicationFactory : IAsyncDisposable, IAsyncLifetime
{
    // Shared key that the Identity test server uses to authenticate internal clients.
    // Must match the value configured on the Identity factory so that InternalClientProvider
    // accepts client_credentials requests for the "internal" scope.
    private const string InternalIdentityKey = "test-internal-identity-key-notifications";

    // IHubProtocol.Name of the MessagePack protocol the service registers in Startup.
    private const string MessagePackProtocolName = "messagepack";

    private readonly IdentityApplicationFactory _identityFactory;
    private readonly WebApplicationFactory<Bit.Notifications.Program> _notificationsFactory;
    private readonly Lazy<Task<string>> _cachedToken;
    private readonly HubInvocationRecorder _recorder = new();

    public NotificationsApplicationFactory()
    {
        _identityFactory = new IdentityApplicationFactory();
        // InternalClientProvider requires SelfHosted = true and a non-empty InternalIdentityKey.
        // A non-empty InstallationId is also required when SelfHosted = true (AddPush validation).
        _identityFactory.UpdateConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "globalSettings:selfHosted", "true" },
                { "globalSettings:internalIdentityKey", InternalIdentityKey },
                { "globalSettings:installation:id", "10000000-0000-0000-0000-000000000000" },
            });
        });

        var (notificationsHubContext, _) = _recorder.CreateHubContext<NotificationsHub>();
        var (anonymousHubContext, _) = _recorder.CreateHubContext<AnonymousNotificationsHub>();

        _notificationsFactory = new WebApplicationFactory<Bit.Notifications.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "OpenTelemetry:Enabled", "false" },
                    // SelfHosted = true activates the [SelfHosted(SelfHostedOnly = true)] filter on
                    // SendController, and skips cloud-only background services at startup.
                    { "globalSettings:selfHosted", "true" },
                    // The host portion of this URI is irrelevant; all backchannel requests (OIDC discovery,
                    // JWKS) are routed directly to the Identity test server via BackchannelHttpHandler.
                    { "globalSettings:baseServiceUri:internalIdentity", "http://localhost" },
                });
            });
            builder.ConfigureTestServices(services =>
            {
                // Route JWT validation to the in-memory Identity test server so tokens issued by
                // _identityFactory are trusted without needing a running external identity service.
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.BackchannelHttpHandler = _identityFactory.Server.CreateHandler();
                });
                // Replace the real SignalR hub contexts with substitutes so tests can assert
                // which user or group each notification was routed to.
                services.AddSingleton(notificationsHubContext);
                services.AddSingleton(anonymousHubContext);
            });
        });

        _cachedToken = new Lazy<Task<string>>(FetchInternalAccessTokenAsync);
    }

    /// <summary>
    /// Returns a Bearer token with <c>scope=internal</c>, satisfying the Notifications service
    /// "Internal" authorization policy. The result is cached for the lifetime of the factory.
    /// </summary>
    public Task<string> GetInternalAccessTokenAsync() => _cachedToken.Value;

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured with a valid Bearer token that satisfies
    /// the "Internal" authorization policy required by <c>POST /send</c>.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await GetInternalAccessTokenAsync();
        var client = _notificationsFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateClient() => _notificationsFactory.CreateClient();

    public async ValueTask DisposeAsync()
    {
        await _notificationsFactory.DisposeAsync();
        _identityFactory.Dispose();
    }

    // Lets xunit own the lifetime when this is used as a class fixture, so the app and its
    // in-memory Identity server boot once per test class instead of once per test case.
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    private async Task<string> FetchInternalAccessTokenAsync()
    {
        using var client = _identityFactory.CreateClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "internal.notifications" },
            { "client_secret", InternalIdentityKey },
            { "scope", "internal" },
        });
        var response = await client.PostAsync("/connect/token", content);
        response.EnsureSuccessStatusCode();
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Waits for the next notification the service routes to either hub and returns it, including
    /// the arguments that would have been serialized and sent to connected clients.
    /// </summary>
    internal Task<HubInvocation> AwaitNextHubInvocationAsync(CancellationToken cancellationToken = default)
        => _recorder.AwaitNextAsync(cancellationToken);

    /// <summary>
    /// Discards notifications recorded so far. Call this before exercising a new one when the factory
    /// is shared across tests, so a case that failed mid-flight cannot desynchronise the next one.
    /// </summary>
    internal void DiscardRecordedHubInvocations() => _recorder.DiscardRecorded();

    /// <summary>
    /// Encodes a notification into the exact bytes the service would put on a client connection,
    /// using the hub protocol the service itself is configured with. Use this to assert on the wire
    /// format clients observe rather than on the intermediate CLR objects.
    /// </summary>
    /// <remarks>
    /// The invocation ID is left unset because hub sends are fire-and-forget — this mirrors the
    /// message SignalR's own lifetime manager builds for <c>SendCoreAsync</c>.
    /// </remarks>
    internal byte[] EncodeForClients(HubInvocation invocation)
    {
        var protocol = _notificationsFactory.Services.GetServices<IHubProtocol>()
            .Single(candidate => candidate.Name == MessagePackProtocolName);

        return protocol.GetMessageBytes(new InvocationMessage(invocation.Method, invocation.Arguments)).ToArray();
    }
}
