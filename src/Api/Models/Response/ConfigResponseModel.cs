// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
        Push = PushSettings.Build(globalSettings);
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
    public PushTechnologyType PushTechnology { get; private init; }
    public string VapidPublicKey { get; private init; }

    public static PushSettings Build(IGlobalSettings globalSettings)
    {
        var vapidPublicKey = globalSettings.WebPush.VapidPublicKey;
        var pushTechnology = vapidPublicKey != null ? PushTechnologyType.WebPush : PushTechnologyType.SignalR;
        return new()
        {
            VapidPublicKey = vapidPublicKey,
            PushTechnology = pushTechnology
        };
    }
}

public class ServerSettingsResponseModel
{
    public bool DisableUserRegistration { get; set; }
}
