using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Admin.Models
{
    public class ProviderEditModel : ProviderViewModel
    {
<<<<<<< HEAD
        public ProviderEditModel(Provider provider, IEnumerable<ProviderUser> providerUsers)
=======
        public ProviderEditModel() { }

        public ProviderEditModel(Provider provider, IEnumerable<ProviderUserUserDetails> providerUsers)
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
            : base(provider, providerUsers)
        {
            Name = provider.Name;
            BusinessName = provider.BusinessName;
            BillingEmail = provider.BillingEmail;
            Enabled = provider.Enabled;
            UseEvents = provider.UseEvents;
        }

        public bool Enabled { get; set; }
        public string BillingEmail { get; set; }
        public string BusinessName { get; set; }
        public string Name { get; set; }
        [Display(Name = "Events")]
        public bool UseEvents { get; set; }
        
        public Provider ToProvider(Provider existingProvider)
        {
            existingProvider.Name = Name;
            existingProvider.BusinessName = BusinessName;
            existingProvider.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
            existingProvider.UseEvents = UseEvents;
            existingProvider.Enabled = Enabled;
            return existingProvider;
        }
    }
}
