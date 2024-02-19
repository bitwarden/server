using Bit.Admin.Models;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Admin.AdminConsole.Models;

public class OrganizationsModel : PagedModel<Organization>
{
    public string Name { get; set; }
    public string UserEmail { get; set; }
    public bool? Paid { get; set; }
    public string Action { get; set; }
    public bool SelfHosted { get; set; }
}
