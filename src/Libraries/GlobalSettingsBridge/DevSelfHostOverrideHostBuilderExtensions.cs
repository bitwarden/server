using Bit.GlobalSettingsBridge;

namespace Microsoft.Extensions.Hosting;

public static class DevSelfHostOverrideHostBuilderExtensions
{
    /// <summary>
    /// In the <c>Development</c> environment, exposes every <c>Dev:SelfHostOverride:GlobalSettings:*</c>
    /// configuration value as <c>GlobalSettings:*</c> when <c>developSelfHosted</c> is <see langword="true"/>,
    /// so running a service locally in self-host mode works for anything reading configuration.
    /// </summary>
    /// <remarks>
    /// The override only sees configuration sources added before this call and takes precedence over all
    /// of them, so call it after every other app configuration call on the builder. Outside of
    /// <c>Development</c> this does nothing.
    /// </remarks>
    /// <param name="hostBuilder">The host builder.</param>
    /// <returns>The host builder for additional chaining.</returns>
    public static IHostBuilder ConfigureDevSelfHostOverride(this IHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            if (!context.HostingEnvironment.IsDevelopment())
            {
                return;
            }

            config.Add(new DevSelfHostOverrideConfigurationSource([.. config.Sources]));
        });
    }
}
