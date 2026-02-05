// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Data;

public class PageOptions
{
    public string ContinuationToken { get; set; }
    public int PageSize { get; set; } = 50;
}
