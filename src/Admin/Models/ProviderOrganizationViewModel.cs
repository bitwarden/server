namespace Bit.Admin.Models;

public class ProviderOrganizationViewModel : PagedModel<ProviderOrganizationItemViewModel>
{
    public Guid ProviderId { get; set; }
    public string OrganizationName { get; set; }
    public string OrganizationOwnerEmail { get; set; }
}
