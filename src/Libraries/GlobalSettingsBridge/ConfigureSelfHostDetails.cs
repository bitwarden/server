using Bitwarden.Server.Sdk.Environment.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bit.GlobalSettingsBridge;

internal sealed class ConfigureSelfHostDetails : IConfigureOptions<SelfHostDetails>
{
    private readonly IConfiguration _config;

    public ConfigureSelfHostDetails(IConfiguration config) => _config = config;

    public void Configure(SelfHostDetails details)
    {
        if (!_config.GetValue<bool>("globalSettings:selfHosted"))
        {
            details.MakeCloud();
            return;
        }

        var flavor = _config.GetValue<bool>("globalSettings:liteDeployment") ? "lite" : "unknown";
        details.MakeSelfHost(flavor);
    }
}
