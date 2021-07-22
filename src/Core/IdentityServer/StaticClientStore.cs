using IdentityServer4.Models;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Settings;
using Bit.Core.Enums;

namespace Bit.Core.IdentityServer
{
    public class StaticClientStore
    {
        public StaticClientStore(GlobalSettings globalSettings)
        {
            ApiClients = new List<Client>
            {
                new ApiClient(globalSettings, BitwardenClient.Mobile, 90, 1),
                new ApiClient(globalSettings, BitwardenClient.Web, 30, 1),
                new ApiClient(globalSettings, BitwardenClient.Browser, 30, 1),
                new ApiClient(globalSettings, BitwardenClient.Desktop, 30, 1),
                new ApiClient(globalSettings, BitwardenClient.Cli, 30, 1),
                new ApiClient(globalSettings, BitwardenClient.DirectoryConnector, 30, 24)
            }.ToDictionary(c => c.ClientId);
        }

        public IDictionary<string, Client> ApiClients { get; private set; }
    }
}
