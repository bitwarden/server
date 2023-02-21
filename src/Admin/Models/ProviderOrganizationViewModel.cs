using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Models;

public class ProviderOrganizationViewModel : PagedModel<ProviderOrganizationItemViewModel>
{
    public Guid ProviderId { get; set; }

    [Display(Name = "Organization Name")]
    public string OrganizationName { get; set; }

    [Display(Name = "Owner Email")]
    public string OrganizationOwnerEmail { get; set; }
}
