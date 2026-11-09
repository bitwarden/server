using BenchmarkDotNet.Attributes;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bit.MicroBenchmarks.Identity.IdentityServer;

public class StaticClientStoreTests
{
    private readonly StaticClientStore _store;

    public StaticClientStoreTests()
    {
        _store = new StaticClientStore(new GlobalSettings(), NullLogger<StaticClientStore>.Instance);
    }

    [Params("mobile", "connector", "invalid", "a_much_longer_invalid_value_that_i_am_making_up", "WEB", "")]
    public string ClientId { get; set; } = null!;

    [Benchmark]
    public Client? TryGetValue()
    {
        return _store.Clients.TryGetValue(ClientId, out var client)
          ? client
          : null;
    }
}
