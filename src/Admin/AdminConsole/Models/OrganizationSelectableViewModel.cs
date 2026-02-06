using Bit.Core.AdminConsole.Entities;

namespace Bit.Admin.AdminConsole.Models;

public class OrganizationSelectableViewModel : Organization
{
    public bool Selected { get; set; }
}
