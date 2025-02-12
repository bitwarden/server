using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Services;
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
    public ServerSettingsResponseModel Settings { get; set; }

    public ConfigResponseModel() : base("config")
    {
        Version = AssemblyHelpers.GetVersion();
        GitHash = AssemblyHelpers.GetGitHash();
        Environment = new EnvironmentConfigResponseModel();
        FeatureStates = new Dictionary<string, object>();
        Settings = new ServerSettingsResponseModel();
    }

    public ConfigResponseModel(
        IFeatureService featureService,
        IGlobalSettings globalSettings
        ) : base("config")
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
        FeatureStates = featureService.GetAll();
        Push = new PushSettings((bool)FeatureStates[FeatureFlagKeys.WebPush], globalSettings);
        Settings = new ServerSettingsResponseModel
        {
            DisableUserRegistration = globalSettings.DisableUserRegistration
        };
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
    private readonly bool _webPushEnabled;
    private readonly string _vapidPublicKey;
    public PushTechnologyType PushTechnology
    {
        get
        {
            if (VapidPublicKey != null)
            {
                return PushTechnologyType.WebPush;
            }
            return PushTechnologyType.SignalR;
        }
    }
    /// <summary>
    /// Only for use when PushTechnology is WebPush.
    /// </summary>
    public string VapidPublicKey
    {
        get
        {
            if (_webPushEnabled)
            {
                return _vapidPublicKey;
            }
            return null;
        }
    }

    public PushSettings(bool webPushEnabled, IGlobalSettings globalSettings)
    {
        _webPushEnabled = webPushEnabled;
        _vapidPublicKey = globalSettings.WebPush.VapidPublicKey;
    }
}

public class ServerSettingsResponseModel
{
    public bool DisableUserRegistration { get; set; }
}
