using System.Net;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bit.GlobalSettingsBridge;

internal sealed class ConfigureForwardedHeadersOptions : IConfigureOptions<ForwardedHeadersOptions>
{
    private readonly IBitwardenEnvironment _environment;
    private readonly IConfiguration _config;

    public ConfigureForwardedHeadersOptions(IBitwardenEnvironment environment, IConfiguration config)
    {
        _environment = environment;
        _config = config;
    }

    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        if (_environment.SelfHostFlavor != "lite")
        {
            // Trust the X-Forwarded-Host header of the nginx docker container
            try
            {
                var nginxIp = Dns.GetHostEntry("nginx")?.AddressList.FirstOrDefault();
                if (nginxIp != null)
                {
                    options.KnownProxies.Add(nginxIp);
                }
            }
            catch
            {
                // Ignore DNS errors
            }
        }

        var knownProxies = _config["globalSettings:knownProxies"];
        if (!string.IsNullOrWhiteSpace(knownProxies))
        {
            foreach (var proxy in knownProxies.Split(','))
            {
                if (IPAddress.TryParse(proxy.Trim(), out var ip))
                {
                    options.KnownProxies.Add(ip);
                }
            }
        }

        var knownNetworks = _config["globalSettings:knownNetworks"];
        if (!string.IsNullOrWhiteSpace(knownNetworks))
        {
            foreach (var network in knownNetworks.Split(','))
            {
                if (System.Net.IPNetwork.TryParse(network.Trim(), out var ipn))
                {
                    options.KnownIPNetworks.Add(ipn);
                }
            }
        }

        if (options.KnownProxies.Count > 1 || options.KnownIPNetworks.Count > 1)
        {
            options.ForwardLimit = null;
        }
    }
}
