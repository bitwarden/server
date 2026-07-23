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

public class RelayPushRegistrationServiceTests
{
    private readonly ApiApplicationFactory _cloudApi;
    private readonly Guid _fakeInstallationId;
    private readonly FakeLogCollector _logCollector;
    private readonly RelayPushRegistrationService _sut;
    private readonly ITestOutputHelper _output;

    public RelayPushRegistrationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _cloudApi = new ApiApplicationFactory();
        _cloudApi.SubstituteService<IPushRegistrationService>(service => { });

        _fakeInstallationId = Guid.NewGuid();

        _cloudApi.Identity.SubstituteService<IInstallationRepository>(service =>
        {
            service.GetByIdAsync(_fakeInstallationId)
                .Returns(new Installation
                {
                    Id = _fakeInstallationId,
                    Key = "test_key",
                    Enabled = true,
                });
        });

        // Replace the NullLoggerFactory so we can capture identity server logs for diagnostics.
        _cloudApi.Identity.ConfigureServices(services =>
        {
            services.RemoveAll<ILoggerFactory>();
            services.AddLogging(b => b.AddFakeLogging());
        });

        // Substitute the SDK feature service so LaunchDarklyClientProvider is never instantiated
        // and no outbound connections to LaunchDarkly are made. In CI, those connections hang
        // because packets are dropped (TCP SYN-timeout ~2 min) rather than refused, stalling the
        // TestHost response-body pipe writer even though the server logs "Request finished 200".
        _cloudApi.Identity.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(
            service => { });

        var cloudApiHttpClient = _cloudApi.CreateClient();
        // Access the identity server's in-process handler directly so we can wrap it with
        // a timing handler. _cloudApi.CreateClient() above already triggered identity server
        // startup (ApiApplicationFactory wires the JWT backchannel to the identity TestServer),
        // so Server is ready here without double-starting.
        var identityBaseHandler = _cloudApi.Identity.Server.CreateHandler();
        var identityTimingHandler = new IdentityTimingHandler(output) { InnerHandler = identityBaseHandler };
        var cloudIdentityHttpClient = new HttpClient(identityTimingHandler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        var httpClientFactory = Substitute.For<IHttpClientFactory>();

        httpClientFactory.CreateClient("client")
            .Returns(cloudApiHttpClient);

        httpClientFactory.CreateClient("identity")
            .Returns(cloudIdentityHttpClient);

        var globalSettings = new GlobalSettings
        {
            PushRelayBaseUri = "http://api.localhost"
        };
        globalSettings.Installation.IdentityUri = "http://identity.localhost";
        globalSettings.Installation.Id = _fakeInstallationId;
        globalSettings.Installation.Key = "test_key";

        var logger = new FakeLogger<RelayPushRegistrationService>();

        _logCollector = logger.Collector;

        _sut = new RelayPushRegistrationService(
            httpClientFactory,
            globalSettings,
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

    private void DumpLogs(string context)
    {
        DumpCollector($"RelayPushRegistrationService logs ({context})", _logCollector);
        DumpCollector($"Identity server logs ({context})", _cloudApi.Identity.GetService<FakeLogCollector>());
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
    private sealed class IdentityTimingHandler(ITestOutputHelper output) : DelegatingHandler
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

    [Fact]
    public async Task MobileData_ShouldNotLogIssues()
    {
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identifier = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();

        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);

        using var pollCts = new CancellationTokenSource();
        var pollTask = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
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
        var mockPushRegistrationService = _cloudApi.GetService<IPushRegistrationService>();
        await mockPushRegistrationService
            .Received(1)
            .CreateOrUpdateRegistrationAsync(
                new PushRegistrationData("PushToken"),
                deviceId: $"{_fakeInstallationId}_{deviceId}",
                userId: $"{_fakeInstallationId}_{userId}",
                identifier: $"{_fakeInstallationId}_{identifier}",
                type: DeviceType.iOS,
                Arg.Is<IEnumerable<string>>(v => v.Single() == $"{_fakeInstallationId}_{organizationId}"),
                installationId
            );
    }
}
