// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Admin.Models;

namespace Bit.Admin.AdminConsole.Models;

public class OrganizationUnassignedToProviderSearchViewModel : PagedModel<OrganizationSelectableViewModel>
{
    [Display(Name = "Organization Name")]
    public string OrganizationName { get; set; }

    [Display(Name = "Owner Email")]
    public string OrganizationOwnerEmail { get; set; }
}
