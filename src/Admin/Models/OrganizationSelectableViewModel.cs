using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Admin.Models;

public class OrganizationSelectableViewModel : Organization
{
    public bool Selected { get; set; }
}
