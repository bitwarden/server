using Bit.Admin.AdminConsole.Models;
using Microsoft.AspNetCore.Components;

namespace Bit.Admin.AdminConsole.Components;

public partial class ViewInformation : ComponentBase
{
    [Parameter] public ProviderViewModel Model { get; set; }
}
