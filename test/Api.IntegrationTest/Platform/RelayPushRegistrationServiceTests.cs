using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Enums;
using Bit.Core.Platform.Installations;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration;
using Bit.Core.Platform.PushRegistration.Internal;
using Bit.Core.Settings;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.Platform;

/// <summary>
/// Shared test fixture that starts the API and identity servers once for the entire
/// <see cref="RelayPushRegistrationServiceTests"/> class. Starting both servers takes ~40 s
/// in CI; without this fixture each test method would pay that cost independently.
/// </summary>
public sealed class RelayPushRegistrationServiceFixture : IDisposable
{
    public ApiApplicationFactory CloudApi { get; }
    public Guid FakeInstallationId { get; }

    /// <summary>The in-process TestHost handler for the API server.</summary>
    /// <remarks>
    /// Tests create a fresh <see cref="HttpClient"/> wrapping this handler per test instance.
    /// A shared <see cref="HttpClient"/> cannot be reused because <see cref="BaseIdentityClientService"/>
    /// sets <see cref="HttpClient.BaseAddress"/> in its constructor, which is forbidden after the
    /// first request has been sent.
    /// </remarks>
    public HttpMessageHandler CloudApiHandler { get; }

    /// <summary>The in-process TestHost handler for the identity server.</summary>
    /// <remarks>Tests wrap this in a per-test <see cref="RelayPushRegistrationServiceTests.IdentityTimingHandler"/>.</remarks>
    public HttpMessageHandler IdentityBaseHandler { get; }

    public GlobalSettings GlobalSettings { get; }

    public RelayPushRegistrationServiceFixture()
    {
        CloudApi = new ApiApplicationFactory();
        CloudApi.SubstituteService<IPushRegistrationService>(service => { });

        FakeInstallationId = Guid.NewGuid();

        CloudApi.Identity.SubstituteService<IInstallationRepository>(service =>
        {
            service.GetByIdAsync(FakeInstallationId)
                .Returns(new Installation
                {
                    Id = FakeInstallationId,
                    Key = "test_key",
                    Enabled = true,
                });
        });

        // Replace the NullLoggerFactory so we can capture identity server logs for diagnostics.
        CloudApi.Identity.ConfigureServices(services =>
        {
            services.RemoveAll<ILoggerFactory>();
            services.AddLogging(b => b.AddFakeLogging());
        });

        // Both the API and identity servers call UseBitwardenSdk() on IHostBuilder, which
        // registers LaunchDarklyClientProvider (makes outbound TCP connections) and OTLP
        // exporters (gRPC channel with exponential-backoff retries). In CI both endpoints are
        // unreachable, causing startup / first-request delays of up to ~136 s each.
        // Apply the same two suppressions to both servers.

        // Substitute the SDK feature service so LaunchDarklyClientProvider is never resolved
        // and no outbound connections to LaunchDarkly are made.
        CloudApi.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(service => { });
        CloudApi.Identity.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(
            service => { });

        // Disable OTLP exporters. UpdateHostConfiguration() adds to IHostBuilder.ConfigureAppConfiguration
        // (HostBuilderContext.Configuration), which is what UseBitwardenSdk()'s AddMetrics() reads
        // when deciding whether to call tracing.AddOtlpExporter(). UpdateConfiguration() would
        // add to the web-host config pipeline instead, which AddMetrics() does NOT read.
        CloudApi.UpdateHostConfiguration("OpenTelemetry:Enabled", "false");
        CloudApi.Identity.UpdateHostConfiguration("OpenTelemetry:Enabled", "false");

        // Trigger startup of both servers. CreateClient() starts the API TestHost, which in turn
        // starts the identity TestHost via the JWT backchannel wired up in ApiApplicationFactory.
        // Discard the returned HttpClient — tests must create their own fresh instances because
        // BaseIdentityClientService sets HttpClient.BaseAddress in its constructor, which throws
        // if the client has already sent a request.
        CloudApi.CreateClient();
        CloudApiHandler = CloudApi.Server.CreateHandler();
        IdentityBaseHandler = CloudApi.Identity.Server.CreateHandler();

        GlobalSettings = new GlobalSettings
        {
            PushRelayBaseUri = "http://api.localhost"
        };
        GlobalSettings.Installation.IdentityUri = "http://identity.localhost";
        GlobalSettings.Installation.Id = FakeInstallationId;
        GlobalSettings.Installation.Key = "test_key";
    }

    public void Dispose() => CloudApi.Dispose();
}

public class RelayPushRegistrationServiceTests : IClassFixture<RelayPushRegistrationServiceFixture>
{
    private readonly RelayPushRegistrationServiceFixture _fixture;
    private readonly FakeLogCollector _logCollector;
    private readonly RelayPushRegistrationService _sut;
    private readonly ITestOutputHelper _output;

    public RelayPushRegistrationServiceTests(
        RelayPushRegistrationServiceFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        // Fresh HttpClient instances per test: BaseIdentityClientService sets .BaseAddress in its
        // constructor, which HttpClient forbids after the first request has been sent. Wrapping the
        // shared handlers in new HttpClient instances each time avoids the InvalidOperationException.
        var cloudApiHttpClient = new HttpClient(fixture.CloudApiHandler);
        var identityTimingHandler = new IdentityTimingHandler(output)
        {
            InnerHandler = fixture.IdentityBaseHandler
        };
        var cloudIdentityHttpClient = new HttpClient(identityTimingHandler);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("client").Returns(cloudApiHttpClient);
        httpClientFactory.CreateClient("identity").Returns(cloudIdentityHttpClient);

        var logger = new FakeLogger<RelayPushRegistrationService>();
        _logCollector = logger.Collector;

        _sut = new RelayPushRegistrationService(
            httpClientFactory,
            fixture.GlobalSettings,
            logger
        );
    }

    [Fact]
    public async Task BrowserExtensionData_ShouldNotLogIssues()
    {
        await _sut.CreateOrUpdateRegistrationAsync(
            new PushRegistrationData("endpoint", "p256dh", "auth"),
            deviceId: Guid.NewGuid().ToString(),
            userId: Guid.NewGuid().ToString(),
            identifier: Guid.NewGuid().ToString(),
            DeviceType.ChromeExtension,
            organizationIds: [Guid.NewGuid().ToString()],
            installationId: Guid.NewGuid()
        );

        var logs = _logCollector.GetSnapshot();

        Assert.DoesNotContain(logs, l => l.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Returns a truncated snippet of the <c>db.statement</c> tag for EF Core activities,
    /// or <see langword="null"/> for all other activity sources.
    /// </summary>
    private static string? DbStatementSnippet(Activity a)
    {
        if (!a.Source.Name.Contains("EntityFrameworkCore"))
        {
            return null;
        }

        var stmt = a.GetTagItem("db.statement")?.ToString();
        if (stmt is null)
        {
            return null;
        }

        const int maxLen = 120;
        return stmt.Length <= maxLen ? stmt : stmt[..maxLen] + "…";
    }

    private void DumpLogs(string context)
    {
        DumpCollector($"RelayPushRegistrationService logs ({context})", _logCollector);
        DumpCollector($"Identity server logs ({context})", _fixture.CloudApi.Identity.GetService<FakeLogCollector>());
    }

    /// <summary>
    /// Wraps the identity server's in-process handler to log the timing of each HTTP phase.
    /// <para>
    /// When the identity server finishes writing the response, the server-side log shows
    /// "Request finished … 200 … Xms". But the client can still hang reading the response
    /// body from the TestHost pipe. This handler reveals which phase stalls:
    /// <list type="bullet">
    ///   <item>If "inner handler returned" never appears, the TestHost pipeline itself is stuck.</item>
    ///   <item>If "inner handler returned" appears but the test still hangs, HttpClient is stuck
    ///         buffering the response body (ResponseContentRead mode) from the pipe.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class IdentityTimingHandler(ITestOutputHelper output) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            output.WriteLine($"[identity→] {request.Method} {request.RequestUri} (sending)");
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                output.WriteLine(
                    $"[identity←] inner handler returned {(int)response.StatusCode} at {sw.ElapsedMilliseconds}ms " +
                    $"(headers ready; HttpClient will now read body)");
                return response;
            }
            catch (Exception ex)
            {
                output.WriteLine(
                    $"[identity✗] {ex.GetType().Name} after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                throw;
            }
        }
    }

    private void DumpCollector(string label, FakeLogCollector collector)
    {
        var logs = collector.GetSnapshot();
        _output.WriteLine($"--- {label}: {logs.Count} entries ---");
        foreach (var log in logs)
        {
            _output.WriteLine($"[{log.Level}] {log.Message}");
            if (log.Exception is not null)
            {
                _output.WriteLine($"  Exception: {log.Exception}");
            }
        }
        _output.WriteLine("---");
    }

    /// <summary>
    /// Subscribes to <see cref="DiagnosticListener.AllListeners"/> and logs every outbound HTTP
    /// request seen on <c>HttpHandlerDiagnosticListener</c>. Because <see cref="DiagnosticListener.AllListeners"/>
    /// replays already-active listeners on subscribe, this captures HTTP calls made by the identity
    /// TestHost even though it started before the subscription was established.
    /// </summary>
    private sealed class DiagnosticAllObserver(ITestOutputHelper output, Stopwatch sw)
        : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];

        public void OnNext(DiagnosticListener listener)
        {
            output.WriteLine($"[+{sw.Elapsed.TotalSeconds:F2}s][DiagListener] {listener.Name}");
            if (listener.Name is "HttpHandlerDiagnosticListener")
            {
                _subscriptions.Add(listener.Subscribe(new OutboundHttpObserver(output, sw)));
            }
        }

        public void OnError(Exception error) { }
        public void OnCompleted() { }

        public void Dispose()
        {
            foreach (var s in _subscriptions) s.Dispose();
        }
    }

    /// <summary>
    /// Observes <c>HttpHandlerDiagnosticListener</c> events and logs each outbound HTTP
    /// request, response, and exception with a timestamp relative to the test stopwatch.
    /// </summary>
    private sealed class OutboundHttpObserver(ITestOutputHelper output, Stopwatch sw)
        : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> kv)
        {
            try
            {
                switch (kv.Key)
                {
                    case "System.Net.Http.HttpRequestOut.Start":
                    {
                        var req = GetProperty<HttpRequestMessage>(kv.Value, "Request");
                        output.WriteLine($"[+{sw.Elapsed.TotalSeconds:F2}s][HTTP→] {req?.Method} {req?.RequestUri}");
                        break;
                    }
                    case "System.Net.Http.HttpRequestOut.Stop":
                    {
                        var req = GetProperty<HttpRequestMessage>(kv.Value, "Request");
                        var res = GetProperty<HttpResponseMessage>(kv.Value, "Response");
                        output.WriteLine($"[+{sw.Elapsed.TotalSeconds:F2}s][HTTP←] {req?.Method} {req?.RequestUri} → {(int?)res?.StatusCode}");
                        break;
                    }
                    case "System.Net.Http.Exception":
                    {
                        var req = GetProperty<HttpRequestMessage>(kv.Value, "Request");
                        var ex = GetProperty<Exception>(kv.Value, "Exception");
                        output.WriteLine($"[+{sw.Elapsed.TotalSeconds:F2}s][HTTP✗] {req?.Method} {req?.RequestUri}: {ex?.GetType().Name}: {ex?.Message}");
                        break;
                    }
                }
            }
            catch { /* never throw in a diagnostic observer */ }
        }

        private static T? GetProperty<T>(object? obj, string name) where T : class
            => obj?.GetType().GetProperty(name)?.GetValue(obj) as T;

        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public async Task MobileData_ShouldNotLogIssues()
    {
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identifier = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();

        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);

        var sw = Stopwatch.StartNew();

        // Subscribe to all active and future DiagnosticListeners. The replay-on-subscribe
        // semantics mean we capture listeners that already exist (e.g. HttpHandlerDiagnosticListener,
        // which was created during app startup). Because the identity TestHost runs in-process,
        // outbound HttpClient calls made by the identity server during request processing appear here.
        using var diagObserver = new DiagnosticAllObserver(_output, sw);
        using var diagSub = DiagnosticListener.AllListeners.Subscribe(diagObserver);

        // Subscribe to all ActivitySource spans. Together with the outbound-HTTP events above,
        // this pinpoints which operation within the identity server pipeline stalls.
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a =>
            {
                var url = a.GetTagItem("url.full")?.ToString()
                       ?? a.GetTagItem("http.url")?.ToString()
                       ?? a.GetTagItem("server.address")?.ToString();
                // db.statement may already be set as an initial tag on EF Core activities.
                var dbSnippet = DbStatementSnippet(a);
                _output.WriteLine(
                    $"[+{sw.Elapsed.TotalSeconds:F2}s][Activity+] {a.Source.Name}/{a.OperationName}"
                    + (url is not null ? $" {url}" : "")
                    + (dbSnippet is not null ? $" sql={dbSnippet}" : ""));
            },
            ActivityStopped = a =>
            {
                var url = a.GetTagItem("url.full")?.ToString()
                       ?? a.GetTagItem("http.url")?.ToString();
                // db.statement is guaranteed to be set by the time the activity stops.
                var dbSnippet = DbStatementSnippet(a);
                _output.WriteLine(
                    $"[+{sw.Elapsed.TotalSeconds:F2}s][Activity-] {a.Source.Name}/{a.OperationName}"
                    + $" {a.Duration.TotalMilliseconds:F0}ms"
                    + (url is not null ? $" {url}" : "")
                    + (dbSnippet is not null ? $" sql={dbSnippet}" : ""));
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var pollCts = new CancellationTokenSource();
        var pollTask = Task.Run(async () =>
        {
            while (!pollCts.Token.IsCancellationRequested)
            {
                ThreadPool.GetAvailableThreads(out var w, out var io);
                _output.WriteLine($"[+{sw.Elapsed.TotalSeconds:F1}s] ThreadPool: {w}/{maxWorkers} workers, {io}/{maxIo} IO available");
                await Task.Delay(TimeSpan.FromSeconds(5), pollCts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }, pollCts.Token);

        await _sut.CreateOrUpdateRegistrationAsync(
            new PushRegistrationData("PushToken"),
            deviceId.ToString(),
            userId.ToString(),
            identifier.ToString(),
            DeviceType.iOS,
            [organizationId.ToString()],
            installationId
        );

        await pollCts.CancelAsync();
        await pollTask;

        var logs = _logCollector.GetSnapshot();

        DumpLogs("after CreateOrUpdateRegistrationAsync");

        Assert.DoesNotContain(logs, l => l.Level >= LogLevel.Warning);

        // Mobile should also actually successfully make it to the cloud push registration service
        // with all of its data prefixed with the self host installation id.
        var mockPushRegistrationService = _fixture.CloudApi.GetService<IPushRegistrationService>();
        await mockPushRegistrationService
            .Received(1)
            .CreateOrUpdateRegistrationAsync(
                new PushRegistrationData("PushToken"),
                deviceId: $"{_fixture.FakeInstallationId}_{deviceId}",
                userId: $"{_fixture.FakeInstallationId}_{userId}",
                identifier: $"{_fixture.FakeInstallationId}_{identifier}",
                type: DeviceType.iOS,
                Arg.Is<IEnumerable<string>>(v => v.Single() == $"{_fixture.FakeInstallationId}_{organizationId}"),
                installationId
            );
    }
}
