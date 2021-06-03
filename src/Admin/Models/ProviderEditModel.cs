using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Admin.Models
{
    public class ProviderEditModel : ProviderViewModel
    {
        public ProviderEditModel(Provider provider, IEnumerable<ProviderUser> providerUsers)
            : base(provider, providerUsers)
        {
            Name = provider.Name;
            BusinessName = provider.BusinessName;
            BillingEmail = provider.BillingEmail;
            Enabled = provider.Enabled;
        }

        public string Administrators { get; set; }

        public bool Enabled { get; set; }

        public string BillingEmail { get; set; }

        public string BusinessName { get; set; }

        public string Name { get; set; }
    }
}
