using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Bit.GlobalSettingsBridge;

/// <summary>
/// Exposes <c>Dev:SelfHostOverride:GlobalSettings:*</c> keys as <c>GlobalSettings:*</c> when
/// <c>developSelfHosted</c> is <see langword="true"/>.
/// </summary>
/// <remarks>
/// SharedWeb's <c>GlobalSettingsServiceCollectionExtensions</c> detects this provider by its full type name to skip
/// its own copy of the override, so renaming or moving this type requires updating it there too.
/// </remarks>
internal sealed class DevSelfHostOverrideConfigurationProvider : ConfigurationProvider, IDisposable
{
    private const string GlobalSettingsSection = "GlobalSettings";
    private const string OverrideSection = "Dev:SelfHostOverride:GlobalSettings";

    private readonly IConfigurationRoot _configuration;
    private readonly IDisposable _reloadRegistration;

    public DevSelfHostOverrideConfigurationProvider(IConfigurationRoot configuration)
    {
        _configuration = configuration;
        _reloadRegistration = ChangeToken.OnChange(_configuration.GetReloadToken, () =>
        {
            Load();
            OnReload();
        });
    }

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (_configuration.GetValue<bool>("developSelfHosted"))
        {
            foreach (var (key, value) in _configuration.GetSection(OverrideSection).AsEnumerable(makePathsRelative: true))
            {
                if (value is not null)
                {
                    data[ConfigurationPath.Combine(GlobalSettingsSection, key)] = value;
                }
            }
        }

        Data = data;
    }

    public void Dispose()
    {
        _reloadRegistration.Dispose();
        (_configuration as IDisposable)?.Dispose();
    }
}
