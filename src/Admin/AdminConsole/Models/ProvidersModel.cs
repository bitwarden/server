using Bit.Admin.Models;
using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Admin.AdminConsole.Models;

public class ProvidersModel : PagedModel<Provider>
{
    public string Name { get; set; }
    public string UserEmail { get; set; }
    public bool? Paid { get; set; }
    public string Action { get; set; }
    public bool SelfHosted { get; set; }
}
