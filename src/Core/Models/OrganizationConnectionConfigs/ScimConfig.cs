using Bit.Core.Enums;

namespace Bit.Core.Models.OrganizationConnectionConfigs
{
    public class ScimConfig
    {
        public bool Enabled { get; set; }
        public ScimProviderType ScimProvider { get; set; }
        public string ServiceUrl { get; set; }
    }
}
