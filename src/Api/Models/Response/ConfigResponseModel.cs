using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class ConfigResponseModel : ResponseModel
{
    public string Version { get; set; }
    public string GitHash { get; set; }
    public ServerConfigResponseModel Server { get; set; }
    public EnvironmentConfigResponseModel Environment { get; set; }
    public IDictionary<string, object> FeatureStates { get; set; }
    public PushSettings Push { get; set; }

    public ConfigResponseModel(
        IGlobalSettings globalSettings,
        IDictionary<string, object> featureStates) : base("config")
    {
        Version = AssemblyHelpers.GetVersion();
        GitHash = AssemblyHelpers.GetGitHash();
        Environment = new EnvironmentConfigResponseModel
        {
            CloudRegion = globalSettings.BaseServiceUri.CloudRegion,
            Vault = globalSettings.BaseServiceUri.Vault,
            Api = globalSettings.BaseServiceUri.Api,
            Identity = globalSettings.BaseServiceUri.Identity,
            Notifications = globalSettings.BaseServiceUri.Notifications,
            Sso = globalSettings.BaseServiceUri.Sso
        };
        FeatureStates = featureStates;
        Push = new PushSettings(globalSettings);
    }
}

public class ServerConfigResponseModel
{
    public string Name { get; set; }
    public string Url { get; set; }
}

public class EnvironmentConfigResponseModel
{
    public string CloudRegion { get; set; }
    public string Vault { get; set; }
    public string Api { get; set; }
    public string Identity { get; set; }
    public string Notifications { get; set; }
    public string Sso { get; set; }
}

public class PushSettings
{
    public PushTechnologyType PushTechnology { get; set; }
    /// <summary>
    /// Only for use when PushTechnology is WebPush.
    /// </summary>
    public string VapidPublicKey { get; set; }

    public PushSettings()
    {
    }
    public PushSettings(IGlobalSettings globalSettings)
    {
        VapidPublicKey = globalSettings.WebPush.VapidPublicKey;
        PushTechnology = globalSettings.WebPush.SupportsWebPush
            ? PushTechnologyType.WebPush
            : PushTechnologyType.SignalR;
    }
}
