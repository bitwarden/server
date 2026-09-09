using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Notifications.Test;

/// <summary>
/// Wraps a <see cref="WebApplicationFactory{TEntryPoint}"/> for the Notifications service alongside
/// an in-memory Identity server that issues real JWT tokens. Tests interact with the service through
/// <see cref="HttpClient"/> only.
/// </summary>
public sealed class NotificationsApplicationFactory : IAsyncDisposable
{
    // Shared key that the Identity test server uses to authenticate internal clients.
    // Must match the value configured on the Identity factory so that InternalClientProvider
    // accepts client_credentials requests for the "internal" scope.
    private const string InternalIdentityKey = "test-internal-identity-key-notifications";

    private readonly IdentityApplicationFactory _identityFactory;
    private readonly WebApplicationFactory<Bit.Notifications.Program> _notificationsFactory;
    private readonly Lazy<Task<string>> _cachedToken;

    /// <summary>
    /// The mock <see cref="IHubClients"/> wired into <see cref="NotificationsHub"/>. Use this to
    /// assert that <c>POST /send</c> routed a notification to the expected user or group.
    /// </summary>
    public IHubClients NotificationsHubClients { get; }

    /// <summary>
    /// The mock <see cref="IHubClients"/> wired into <see cref="AnonymousNotificationsHub"/>. Use
    /// this to assert that <c>POST /send</c> routed a notification to the expected anonymous group
    /// (e.g. <see cref="Bit.Core.Enums.PushType.AuthRequestResponse"/>).
    /// </summary>
    public IHubClients AnonymousHubClients { get; }

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

        var (notificationsHubContext, notificationsClients) = BuildHubContext<NotificationsHub>();
        NotificationsHubClients = notificationsClients;
        var (anonymousHubContext, anonymousClients) = BuildHubContext<AnonymousNotificationsHub>();
        AnonymousHubClients = anonymousClients;

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

    // Builds a substitute IHubContext<THub> whose Clients property captures routing calls so
    // tests can assert on which user or group received a notification.
    private static (IHubContext<THub> Context, IHubClients Clients) BuildHubContext<THub>()
        where THub : Hub
    {
        var proxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.User(Arg.Any<string>()).Returns(proxy);
        clients.Group(Arg.Any<string>()).Returns(proxy);

        var context = Substitute.For<IHubContext<THub>>();
        context.Clients.Returns(clients);
        return (context, clients);
    }
}
