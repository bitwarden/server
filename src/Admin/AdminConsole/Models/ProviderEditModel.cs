using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Admin.AdminConsole.Models;

public class ProviderEditModel : ProviderViewModel
{
    public ProviderEditModel() { }

    public ProviderEditModel(Provider provider, IEnumerable<ProviderUserUserDetails> providerUsers, IEnumerable<ProviderOrganizationOrganizationDetails> organizations)
        : base(provider, providerUsers, organizations)
    {
        Name = provider.Name;
        BusinessName = provider.BusinessName;
        BillingEmail = provider.BillingEmail;
        BillingPhone = provider.BillingPhone;
    }

    [Display(Name = "Billing Email")]
    public string BillingEmail { get; set; }
    [Display(Name = "Billing Phone Number")]
    public string BillingPhone { get; set; }
    [Display(Name = "Business Name")]
    public string BusinessName { get; set; }
    public string Name { get; set; }
    [Display(Name = "Events")]

    public Provider ToProvider(Provider existingProvider)
    {
        existingProvider.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
        existingProvider.BillingPhone = BillingPhone?.ToLowerInvariant()?.Trim();
        return existingProvider;
    }
}
