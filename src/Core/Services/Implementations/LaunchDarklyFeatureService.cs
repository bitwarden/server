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

    public Dictionary<string, object> GetAll(ICurrentContext currentContext)
    {
        var results = new Dictionary<string, object>();

        var keys = FeatureFlagKeys.GetAllKeys();

        var values = _client.AllFlagsState(BuildContext(currentContext));
        if (values.Valid)
        {
            foreach (var key in keys)
            {
                var value = values.GetFlagValueJson(key);
                switch (value.Type)
                {
                    case LaunchDarkly.Sdk.LdValueType.Bool:
                        results.Add(key, value.AsBool);
                        break;

                    case LaunchDarkly.Sdk.LdValueType.Number:
                        results.Add(key, value.AsInt);
                        break;

                    case LaunchDarkly.Sdk.LdValueType.String:
                        results.Add(key, value.AsString);
                        break;
                }
            }
        }

        return results;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private LaunchDarkly.Sdk.Context BuildContext(ICurrentContext currentContext)
    {
        var builder = LaunchDarkly.Sdk.Context.MultiBuilder();

        switch (currentContext.ClientType)
        {
            case Identity.ClientType.User:
                {
                    var ldUser = LaunchDarkly.Sdk.Context.Builder(currentContext.UserId.Value.ToString());
                    ldUser.Kind(LaunchDarkly.Sdk.ContextKind.Default);

                    if (currentContext.Organizations?.Any() ?? false)
                    {
                        var ldOrgs = currentContext.Organizations.Select(o => LaunchDarkly.Sdk.LdValue.Of(o.Id.ToString()));
                        ldUser.Set("organizations", LaunchDarkly.Sdk.LdValue.ArrayFrom(ldOrgs));
                    }

                    builder.Add(ldUser.Build());
                }
                break;

            case Identity.ClientType.Organization:
                {
                    var ldOrg = LaunchDarkly.Sdk.Context.Builder(currentContext.OrganizationId.Value.ToString());
                    ldOrg.Kind("organization");
                    builder.Add(ldOrg.Build());
                }
                break;

            case Identity.ClientType.ServiceAccount:
                {
                    var ldServiceAccount = LaunchDarkly.Sdk.Context.Builder(currentContext.UserId.Value.ToString());
                    ldServiceAccount.Kind("service-account");
                    builder.Add(ldServiceAccount.Build());

                    var ldOrg = LaunchDarkly.Sdk.Context.Builder(currentContext.OrganizationId.Value.ToString());
                    ldOrg.Kind("organization");
                    builder.Add(ldOrg.Build());
                }
                break;
        }

        return builder.Build();
    }
}
