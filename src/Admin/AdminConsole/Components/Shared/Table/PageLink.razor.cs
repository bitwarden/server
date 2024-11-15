using Microsoft.AspNetCore.Components;

namespace Bit.Admin.AdminConsole.Components.Shared.Table;

public partial class PageLink : ComponentBase
{
    [Parameter]
    public string Label { get; set; }

    [Parameter]
    public string? FormKey { get; set; }

    [Parameter]
    public int? Page { get; set; }
}
