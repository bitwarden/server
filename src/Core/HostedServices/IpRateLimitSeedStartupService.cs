using AspNetCoreRateLimit;
using Microsoft.Extensions.Hosting;

namespace Bit.Core.HostedServices;

/// <summary>
/// A startup service that will seed the IP rate limiting stores with any values in the
/// GlobalSettings configuration.
/// </summary>
/// <remarks>
/// <para>Using an <see cref="IHostedService"/> here because it runs before the request processing pipeline
/// is configured, so that any rate limiting configuration is seeded/applied before any requests come in.
/// </para>
/// <para>
/// This is a cleaner alternative to modifying Program.cs in every project that requires rate limiting as
/// described/suggested here:
/// https://github.com/stefanprodan/AspNetCoreRateLimit/wiki/Version-3.0.0-Breaking-Changes
/// </para>
/// </remarks>
public class IpRateLimitSeedStartupService : IHostedService
{
    private readonly IIpPolicyStore _ipPolicyStore;
    private readonly IClientPolicyStore _clientPolicyStore;

    public IpRateLimitSeedStartupService(IIpPolicyStore ipPolicyStore, IClientPolicyStore clientPolicyStore)
    {
        _ipPolicyStore = ipPolicyStore;
        _clientPolicyStore = clientPolicyStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Seed the policies from GlobalSettings
        await _ipPolicyStore.SeedAsync();
        await _clientPolicyStore.SeedAsync();
    }

    // noop
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
