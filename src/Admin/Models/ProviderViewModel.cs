using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Admin.Models
{
    public class ProviderViewModel
    {
        public ProviderViewModel(Provider provider, IEnumerable<ProviderUser> providerUsers)
        {
            Provider = provider;
            UserCount = providerUsers.Count();

            Administrators = string.Join(", ",
                providerUsers
                    .Where(u => u.Type == ProviderUserType.Administrator && u.Status == ProviderUserStatusType.Confirmed)
                    .Select(u => u.Email));
        }

        public int UserCount { get; set; }

        public Provider Provider { get; set; }
        
        public string Administrators { get; set; }
    }
}
