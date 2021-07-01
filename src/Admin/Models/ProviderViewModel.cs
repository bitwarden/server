using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table.Provider;

namespace Bit.Admin.Models
{
    public class ProviderViewModel
    {
        public ProviderViewModel() { }

        public ProviderViewModel(Provider provider, IEnumerable<ProviderUserUserDetails> providerUsers)
        {
            Provider = provider;
            UserCount = providerUsers.Count();

            ProviderAdmins = string.Join(", ",
                providerUsers
                    .Where(u => u.Type == ProviderUserType.ProviderAdmin && u.Status == ProviderUserStatusType.Confirmed)
                    .Select(u => u.Email));
        }

        public int UserCount { get; set; }

        public Provider Provider { get; set; }
        
        public string ProviderAdmins { get; set; }
    }
}
