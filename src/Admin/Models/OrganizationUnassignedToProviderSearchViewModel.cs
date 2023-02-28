using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Models;

public class OrganizationUnassignedToProviderSearchViewModel : PagedModel<OrganizationSelectableViewModel>
{
    [Display(Name = "Organization Name")]
    public string OrganizationName { get; set; }

    [Display(Name = "Owner Email")]
    public string OrganizationOwnerEmail { get; set; }
}
