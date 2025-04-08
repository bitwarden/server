using Microsoft.AspNetCore.Components;

namespace Bit.Admin.Components;

public partial class BitPage : ComponentBase
{
    [Parameter]
    public required string Title { get; set; }

    [Parameter] public bool HideTitle { get; set; } = false;

    [Parameter]
    public required RenderFragment ChildContent { get; set; }

    private string? TitleClass => HideTitle ? "sr-only" : null;
}
