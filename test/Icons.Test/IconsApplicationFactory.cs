using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Bit.Icons.Test;

/// <summary>
/// Wraps a <see cref="WebApplicationFactory{TEntryPoint}"/> for the Icons service and exposes
/// a single <see cref="CreateClient"/> method. Tests interact with the service through
/// <see cref="HttpClient"/> only.
/// </summary>
public sealed class IconsApplicationFactory : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public IconsApplicationFactory(bool selfHosted = false, string vaultOrigin = "https://vault.example.com")
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "GlobalSettings:SelfHosted", selfHosted ? "true" : "false" },
                    { "GlobalSettings:BaseServiceUri:Vault", vaultOrigin },
                    { "OpenTelemetry:Enabled", "false" },
                });
            });
        });
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
