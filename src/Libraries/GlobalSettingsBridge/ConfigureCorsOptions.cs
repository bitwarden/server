using Bitwarden.Server.Sdk.Environment;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bit.GlobalSettingsBridge;

internal sealed class ConfigureCorsOptions : IConfigureOptions<CorsOptions>
{
    private readonly IBitwardenEnvironment _environment;
    private readonly IConfiguration _config;

    public ConfigureCorsOptions(IBitwardenEnvironment environment, IConfiguration config)
    {
        _environment = environment;
        _config = config;
    }

    public void Configure(CorsOptions options)
    {
        var vaultUri = _config["globalSettings:baseServiceUri:vault"];
        var selfHosted = _environment.SelfHosted;

        options.AddDefaultPolicy(policy =>
            policy
                .SetIsOriginAllowed(origin =>
                    origin == vaultUri ||
                    origin == "file://" ||
                    origin == "bw-desktop-file://bundle" ||
                    (!selfHosted && origin == "https://bitwarden.com"))
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    }
}
