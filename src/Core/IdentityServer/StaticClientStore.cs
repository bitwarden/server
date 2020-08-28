using IdentityServer4.Models;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.IdentityServer
{
    public class StaticClientStore
    {
        public StaticClientStore(GlobalSettings globalSettings)
        {
            ApiClients = new List<Client>
            {
                new ApiClient(globalSettings, "mobile", 90, 1),
                new ApiClient(globalSettings, "web", 30, 1),
                new ApiClient(globalSettings, "browser", 30, 1),
                new ApiClient(globalSettings, "desktop", 30, 1),
                new ApiClient(globalSettings, "cli", 30, 1),
                new ApiClient(globalSettings, "connector", 30, 24)
            }.ToDictionary(c => c.ClientId);
        }

        public IDictionary<string, Client> ApiClients { get; private set; }
    }
}
