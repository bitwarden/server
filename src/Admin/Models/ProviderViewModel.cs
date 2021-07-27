using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Admin.Models
{
    public class ProviderViewModel
    {
<<<<<<< HEAD
        public ProviderViewModel(Provider provider, IEnumerable<ProviderUser> providerUsers)
=======
        public ProviderViewModel() { }

        public ProviderViewModel(Provider provider, IEnumerable<ProviderUserUserDetails> providerUsers)
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
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
