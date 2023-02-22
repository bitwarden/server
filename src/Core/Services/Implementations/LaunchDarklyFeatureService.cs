using Bit.Core.Settings;
using LaunchDarkly.Sdk.Server;

namespace Bit.Core.Services;

public class LaunchDarklyFeatureService : IFeatureService, IDisposable
{
    private readonly LdClient _client;

    public LaunchDarklyFeatureService(
        GlobalSettings globalSettings)
    {
        var ldConfig = Configuration.Builder(globalSettings.LaunchDarkly?.SdkKey)
                .Offline(globalSettings.SelfHosted)
                .Build();
        _client = new LdClient(ldConfig);
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
