namespace Bit.Core.Models.Data;

public class PageOptions
{
    public string ContinuationToken { get; set; }
    public int PageSize { get; set; } = 50;
}
