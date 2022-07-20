using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response
{
    public class ConfigResponseModel : ResponseModel
    {
        public ConfigResponseModel(string obj = "config") : base(obj)
        {
            this.Version = CoreHelpers.GetVersion();
        }

        public string Version { get; set; }
        public string GitHash { get; set; }
        public ServerConfigResponseModel Server { get; set; }
        public EnvironmentConfigResponseModel Environment { get; set; }
    }

    public class ServerConfigResponseModel
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class EnvironmentConfigResponseModel
    {
        public string Api { get; set; }
    }
}
