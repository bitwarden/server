// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Admin.Models;
using Bit.Core.Providers.Entities;

namespace Bit.Admin.AdminConsole.Models;

public class ProvidersModel : PagedModel<Provider>
{
    public string Name { get; set; }
    public string UserEmail { get; set; }
    public bool? Paid { get; set; }
    public string Action { get; set; }
    public bool SelfHosted { get; set; }
}
