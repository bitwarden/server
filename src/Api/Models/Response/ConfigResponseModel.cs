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

    public ConfigResponseModel(string obj = "config") : base(obj)
    {
        Version = AssemblyHelpers.GetVersion();
        GitHash = AssemblyHelpers.GetGitHash();
        Environment = new EnvironmentConfigResponseModel();
    }

    public ConfigResponseModel(IGlobalSettings globalSettings, string obj = "config") : base(obj)
    {
        Version = AssemblyHelpers.GetVersion();
        GitHash = AssemblyHelpers.GetGitHash();
        Environment = new EnvironmentConfigResponseModel
        {
            Vault = globalSettings.BaseServiceUri.Vault,
            Api = globalSettings.BaseServiceUri.Api,
            Identity = globalSettings.BaseServiceUri.Identity,
            Notifications = globalSettings.BaseServiceUri.Notifications,
            Sso = globalSettings.BaseServiceUri.Sso
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
    public string Vault { get; set; }
    public string Api { get; set; }
    public string Identity { get; set; }
    public string Notifications { get; set; }
    public string Sso { get; set; }
}
