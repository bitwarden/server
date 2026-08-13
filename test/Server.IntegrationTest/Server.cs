using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Bit.Server.IntegrationTest;

public class Server : WebApplicationFactory<Program>
{
    public string? ContentRoot { get; set; }
    public string? WebRoot { get; set; }
    public bool ServeUnknown { get; set; }
    public bool? WebVault { get; set; }
    public string? AppIdLocation { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        var config = new Dictionary<string, string?>
        {
            {"contentRoot", ContentRoot},
            {"webRoot", WebRoot},
            {"serveUnknown", ServeUnknown.ToString().ToLowerInvariant()},
        };

        if (WebVault.HasValue)
        {
            config["webVault"] = WebVault.Value.ToString().ToLowerInvariant();
        }

        if (!string.IsNullOrEmpty(AppIdLocation))
        {
            config["appIdLocation"] = AppIdLocation;
        }

        builder.UseConfiguration(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
    }
}
