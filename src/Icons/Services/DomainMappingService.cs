using System.Collections.Generic;

namespace Bit.Icons.Services
{
    public class DomainMappingService : IDomainMappingService
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>
        {
            ["vault.bitwarden.com"] = "bitwarden.com",
            ["accounts.google.com"] = "google.com",
            // TODO: Add others here
        };

        public string MapDomain(string hostname)
        {
            if(_map.ContainsKey(hostname))
            {
                return _map[hostname];
            }

            return hostname;
        }
    }
}
