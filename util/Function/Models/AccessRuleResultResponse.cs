namespace Bit.Function.Models
{
    public class AccessRuleResultResponse
    {
        public string Id { get; set; }
        public string Notes { get; set; }
        public ConfigurationResponse Configuration { get; set; }

        public class ConfigurationResponse
        {
            public string Target { get; set; }
            public string Value { get; set; }
        }
    }
}
