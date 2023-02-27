using Bit.Core.Settings;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Integrations;

namespace Bit.Core.Services;

public class LaunchDarklyFeatureService : IFeatureService, IDisposable
{
    private readonly LdClient _client;

    public LaunchDarklyFeatureService(
        GlobalSettings globalSettings)
    {
        var ldConfig = Configuration.Builder(globalSettings.LaunchDarkly?.SdkKey);

        if (string.IsNullOrEmpty(globalSettings.LaunchDarkly?.SdkKey))
        {
            // support a file to load flag values
            const string flagOverridePath = "flags.json";
            if (File.Exists(flagOverridePath))
            {
                ldConfig.DataSource(
                    FileData.DataSource()
                        .FilePaths(flagOverridePath)
                        .AutoUpdate(true)
                );
            }

            // do not provide analytics events
            ldConfig.Events(Components.NoEvents);
        }

        // when self-hosted, work entirely offline
        if (globalSettings.SelfHosted)
        {
            ldConfig.Offline(true);
        }

        _client = new LdClient(ldConfig.Build());
    }

    public bool IsOnline()
    {
        return _client.Initialized && !_client.IsOffline();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
