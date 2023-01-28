using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;

namespace Bit.Admin.Models;

public class ProviderEditModel : ProviderViewModel
{
    public ProviderEditModel() { }

    public ProviderEditModel(Provider provider, IEnumerable<ProviderUserUserDetails> providerUsers, IEnumerable<ProviderOrganizationOrganizationDetails> organizations)
        : base(provider, providerUsers, organizations)
    {
        Name = provider.Name;
        BusinessName = provider.BusinessName;
        BillingEmail = provider.BillingEmail;
    }

    [Display(Name = "Billing Email")]
    public string BillingEmail { get; set; }
    [Display(Name = "Business Name")]
    public string BusinessName { get; set; }
    public string Name { get; set; }
    [Display(Name = "Events")]

    public Provider ToProvider(Provider existingProvider)
    {
        existingProvider.Name = Name;
        existingProvider.BusinessName = BusinessName;
        existingProvider.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
        return existingProvider;
    }
}
