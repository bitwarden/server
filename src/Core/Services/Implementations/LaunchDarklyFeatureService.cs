using Bit.Core.Context;
using Bit.Core.Settings;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Integrations;

namespace Bit.Core.Services;

public class LaunchDarklyFeatureService : IFeatureService, IDisposable
{
    private readonly LdClient _client;

    public LaunchDarklyFeatureService(
        IGlobalSettings globalSettings)
    {
        var ldConfig = Configuration.Builder(globalSettings.LaunchDarkly?.SdkKey);

        if (string.IsNullOrEmpty(globalSettings.LaunchDarkly?.SdkKey))
        {
            // support a file to load flag values
            if (File.Exists(globalSettings.LaunchDarkly?.FlagDataFilePath))
            {
                ldConfig.DataSource(
                    FileData.DataSource()
                        .FilePaths(globalSettings.LaunchDarkly?.FlagDataFilePath)
                        .AutoUpdate(true)
                );

                // do not provide analytics events
                ldConfig.Events(Components.NoEvents);
            }
            else
            {
                // when a file-based fallback isn't available, work offline
                ldConfig.Offline(true);
            }
        }
        else if (globalSettings.SelfHosted)
        {
            // when self-hosted, work offline
            ldConfig.Offline(true);
        }

        _client = new LdClient(ldConfig.Build());
    }

    public bool IsOnline()
    {
        return _client.Initialized && !_client.IsOffline();
    }

    public bool IsEnabled(string key, ICurrentContext currentContext, bool defaultValue = false)
    {
        return _client.BoolVariation(key, BuildContext(currentContext), defaultValue);
    }

    public int GetIntVariation(string key, ICurrentContext currentContext, int defaultValue = 0)
    {
        return _client.IntVariation(key, BuildContext(currentContext), defaultValue);
    }

    public string GetStringVariation(string key, ICurrentContext currentContext, string defaultValue = null)
    {
        return _client.StringVariation(key, BuildContext(currentContext), defaultValue);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private LaunchDarkly.Sdk.Context BuildContext(ICurrentContext currentContext)
    {
        var builder = LaunchDarkly.Sdk.Context.MultiBuilder();

        if (currentContext.UserId.HasValue)
        {
            var user = LaunchDarkly.Sdk.Context.Builder(currentContext.UserId.Value.ToString());
            user.Kind(LaunchDarkly.Sdk.ContextKind.Default);
            builder.Add(user.Build());
        }

        if (currentContext.OrganizationId.HasValue)
        {
            var org = LaunchDarkly.Sdk.Context.Builder(currentContext.OrganizationId.Value.ToString());
            org.Kind("org");
            builder.Add(org.Build());
        }

        return builder.Build();
    }
}
