using Bit.Core.Context;
using Bit.Core.Identity;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace Bit.Core.Services;

public class LaunchDarklyFeatureService : IFeatureService
{
    private readonly ILdClient _client;
    private readonly ICurrentContext _currentContext;
    private const string _anonymousUser = "25a15cac-58cf-4ac0-ad0f-b17c4bd92294";

    private const string _contextKindOrganization = "organization";
    private const string _contextKindServiceAccount = "service-account";

    private const string _contextAttributeClientVersion = "client-version";
    private const string _contextAttributeClientVersionIsPrerelease = "client-version-is-prerelease";
    private const string _contextAttributeDeviceType = "device-type";
    private const string _contextAttributeClientType = "client-type";
    private const string _contextAttributeOrganizations = "organizations";

    public LaunchDarklyFeatureService(
        ILdClient client,
        ICurrentContext currentContext)
    {
        _client = client;
        _currentContext = currentContext;
    }

    public static Configuration GetConfiguredClient(GlobalSettings globalSettings)
    {
        var ldConfig = Configuration.Builder(globalSettings.LaunchDarkly?.SdkKey);
        ldConfig.Logging(Components.Logging().Level(LogLevel.Error));

        if (!string.IsNullOrEmpty(globalSettings.ProjectName))
        {
            ldConfig.ApplicationInfo(Components.ApplicationInfo()
                .ApplicationId(globalSettings.ProjectName)
                .ApplicationName(globalSettings.ProjectName)
                .ApplicationVersion(AssemblyHelpers.GetGitHash() ?? $"v{AssemblyHelpers.GetVersion()}")
                .ApplicationVersionName(AssemblyHelpers.GetVersion())
            );
        }

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
            }
            // support configuration directly from settings
            else if (globalSettings.LaunchDarkly?.FlagValues?.Any() is true)
            {
                ldConfig.DataSource(BuildDataSource(globalSettings.LaunchDarkly.FlagValues));
            }
            // support local overrides
            else if (FeatureFlagKeys.GetLocalOverrideFlagValues()?.Any() is true)
            {
                ldConfig.DataSource(BuildDataSource(FeatureFlagKeys.GetLocalOverrideFlagValues()));
            }
            else
            {
                // when fallbacks aren't available, work offline
                ldConfig.Offline(true);
            }

            // do not provide analytics events
            ldConfig.Events(Components.NoEvents);
        }
        else if (globalSettings.SelfHosted)
        {
            // when self-hosted, work offline
            ldConfig.Offline(true);
        }

        return ldConfig.Build();
    }

    public bool IsOnline()
    {
        return _client.Initialized && !_client.IsOffline();
    }

    public bool IsEnabled(string key, bool defaultValue = false)
    {
        return _client.BoolVariation(key, BuildContext(), defaultValue);
    }

    public int GetIntVariation(string key, int defaultValue = 0)
    {
        return _client.IntVariation(key, BuildContext(), defaultValue);
    }

    public string GetStringVariation(string key, string defaultValue = null)
    {
        return _client.StringVariation(key, BuildContext(), defaultValue);
    }

    public Dictionary<string, object> GetAll()
    {
        var results = new Dictionary<string, object>();

        var keys = FeatureFlagKeys.GetAllKeys();

        var values = _client.AllFlagsState(BuildContext());
        if (values.Valid)
        {
            foreach (var key in keys)
            {
                var value = values.GetFlagValueJson(key);
                switch (value.Type)
                {
                    case LdValueType.Bool:
                        results.Add(key, value.AsBool);
                        break;

                    case LdValueType.Number:
                        results.Add(key, value.AsInt);
                        break;

                    case LdValueType.String:
                        results.Add(key, value.AsString);
                        break;
                }
            }
        }

        return results;
    }

    private LaunchDarkly.Sdk.Context BuildContext()
    {
        void SetCommonContextAttributes(ContextBuilder builder)
        {
            if (_currentContext.ClientVersion != null)
            {
                builder.Set(_contextAttributeClientVersion, _currentContext.ClientVersion.ToString());
                builder.Set(_contextAttributeClientVersionIsPrerelease, _currentContext.ClientVersionIsPrerelease);
            }

            if (_currentContext.DeviceType.HasValue)
            {
                builder.Set(_contextAttributeDeviceType, (int)_currentContext.DeviceType.Value);
                builder.Set(_contextAttributeClientType, (int)DeviceTypes.ToClientType(_currentContext.DeviceType.Value));
            }
        }

        var builder = LaunchDarkly.Sdk.Context.MultiBuilder();

        switch (_currentContext.IdentityClientType)
        {
            case IdentityClientType.User:
                {
                    ContextBuilder ldUser;
                    if (_currentContext.UserId.HasValue)
                    {
                        ldUser = LaunchDarkly.Sdk.Context.Builder(_currentContext.UserId.Value.ToString());
                    }
                    else
                    {
                        // group all unauthenticated activity under one anonymous user key and mark as such
                        ldUser = LaunchDarkly.Sdk.Context.Builder(_anonymousUser);
                        ldUser.Anonymous(true);
                    }

                    ldUser.Kind(ContextKind.Default);
                    SetCommonContextAttributes(ldUser);

                    if (_currentContext.Organizations?.Any() ?? false)
                    {
                        var ldOrgs = _currentContext.Organizations.Select(o => LdValue.Of(o.Id.ToString()));
                        ldUser.Set(_contextAttributeOrganizations, LdValue.ArrayFrom(ldOrgs));
                    }

                    builder.Add(ldUser.Build());
                }
                break;

            case IdentityClientType.Organization:
                {
                    if (_currentContext.OrganizationId.HasValue)
                    {
                        var ldOrg = LaunchDarkly.Sdk.Context.Builder(_currentContext.OrganizationId.Value.ToString());

                        ldOrg.Kind(_contextKindOrganization);
                        SetCommonContextAttributes(ldOrg);

                        builder.Add(ldOrg.Build());
                    }
                }
                break;

            case IdentityClientType.ServiceAccount:
                {
                    if (_currentContext.UserId.HasValue)
                    {
                        var ldServiceAccount = LaunchDarkly.Sdk.Context.Builder(_currentContext.UserId.Value.ToString());

                        ldServiceAccount.Kind(_contextKindServiceAccount);
                        SetCommonContextAttributes(ldServiceAccount);

                        builder.Add(ldServiceAccount.Build());
                    }
                    else if (_currentContext.OrganizationId.HasValue)
                    {
                        var ldServiceAccount = LaunchDarkly.Sdk.Context.Builder(_currentContext.OrganizationId.Value.ToString());

                        ldServiceAccount.Kind(_contextKindServiceAccount);
                        SetCommonContextAttributes(ldServiceAccount);

                        builder.Add(ldServiceAccount.Build());
                    }
                }
                break;
        }

        return builder.Build();
    }

    private static TestData BuildDataSource(Dictionary<string, string> values)
    {
        var source = TestData.DataSource();
        foreach (var kvp in values)
        {
            if (bool.TryParse(kvp.Value, out var boolValue))
            {
                source.Update(source.Flag(kvp.Key).ValueForAll(LdValue.Of(boolValue)));
            }
            else if (int.TryParse(kvp.Value, out var intValue))
            {
                source.Update(source.Flag(kvp.Key).ValueForAll(LdValue.Of(intValue)));
            }
            else
            {
                source.Update(source.Flag(kvp.Key).ValueForAll(LdValue.Of(kvp.Value)));
            }
        }

        return source;
    }
}
