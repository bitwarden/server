using Microsoft.Extensions.Configuration;

namespace Bit.GlobalSettingsBridge;

internal sealed class DevSelfHostOverrideConfigurationSource : IConfigurationSource
{
    private readonly IReadOnlyList<IConfigurationSource> _precedingSources;

    public DevSelfHostOverrideConfigurationSource(IReadOnlyList<IConfigurationSource> precedingSources)
    {
        _precedingSources = precedingSources;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        // Copy properties so file based sources resolve the same file provider as the host builder.
        var preceding = new ConfigurationBuilder();
        foreach (var (key, value) in builder.Properties)
        {
            preceding.Properties[key] = value;
        }

        foreach (var source in _precedingSources)
        {
            preceding.Add(source);
        }

        return new DevSelfHostOverrideConfigurationProvider(preceding.Build());
    }
}
