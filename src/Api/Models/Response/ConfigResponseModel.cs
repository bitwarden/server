// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
    public CommunicationSettings Communication { get; set; }
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
        var webPushEnabled = FeatureStates.TryGetValue(FeatureFlagKeys.WebPush, out var webPushEnabledValue) ? (bool)webPushEnabledValue : false;
        Push = PushSettings.Build(webPushEnabled, globalSettings);
        Communication = CommunicationSettings.Build(globalSettings);
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

    public static PushSettings Build(bool webPushEnabled, IGlobalSettings globalSettings)
    {
        var vapidPublicKey = webPushEnabled ? globalSettings.WebPush.VapidPublicKey : null;
        var pushTechnology = vapidPublicKey != null ? PushTechnologyType.WebPush : PushTechnologyType.SignalR;
        return new()
        {
            VapidPublicKey = vapidPublicKey,
            PushTechnology = pushTechnology
        };
    }
}

public class CommunicationSettings
{
    public CommunicationBootstrapSettings Bootstrap { get; private init; }

    public static CommunicationSettings Build(IGlobalSettings globalSettings)
    {
        var bootstrap = CommunicationBootstrapSettings.Build(globalSettings);
        return bootstrap == null ? null : new() { Bootstrap = bootstrap };
    }
}

public class CommunicationBootstrapSettings
{
    public string Type { get; private init; }
    public string IdpLoginUrl { get; private init; }
    public string CookieName { get; private init; }
    public string CookieDomain { get; private init; }

    public static CommunicationBootstrapSettings Build(IGlobalSettings globalSettings)
    {
        return globalSettings.Communication?.Bootstrap?.ToLowerInvariant() switch
        {
            "ssocookievendor" => new()
            {
                Type = "ssoCookieVendor",
                IdpLoginUrl = globalSettings.Communication?.SsoCookieVendor?.IdpLoginUrl,
                CookieName = globalSettings.Communication?.SsoCookieVendor?.CookieName,
                CookieDomain = globalSettings.Communication?.SsoCookieVendor?.CookieDomain
            },
            _ => null
        };
    }
}

public class ServerSettingsResponseModel
{
    public bool DisableUserRegistration { get; set; }
}
