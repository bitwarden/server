using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Bit.Server.IntegrationTest;

public class Server : WebApplicationFactory<Program>
{
    public string? ContentRoot { get; set; }
    public string? WebRoot { get; set; }
    public bool ServeUnknown { get; set; }
    public bool? WebVault { get; set; }
    public string? AppIdLocation { get; set; }

    protected override IWebHostBuilder? CreateWebHostBuilder()
    {
        var args = new List<string>
        {
            "/contentRoot",
            ContentRoot ?? "",
            "/webRoot",
            WebRoot ?? "",
            "/serveUnknown",
            ServeUnknown.ToString().ToLowerInvariant(),
        };

        if (WebVault.HasValue)
        {
            args.Add("/webVault");
            args.Add(WebVault.Value.ToString().ToLowerInvariant());
        }

        if (!string.IsNullOrEmpty(AppIdLocation))
        {
            args.Add("/appIdLocation");
            args.Add(AppIdLocation);
        }

        var builder = WebHostBuilderFactory.CreateFromTypesAssemblyEntryPoint<Program>([.. args])
            ?? throw new InvalidProgramException("Could not create builder from assembly.");

        builder.UseSetting("TEST_CONTENTROOT_SERVER", ContentRoot);
        return builder;
    }
}
