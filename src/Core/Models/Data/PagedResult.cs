namespace Bit.Core.Models.Data;

public class PagedResult<T>
{
    public List<T> Data { get; set; } = new List<T>();
    public string ContinuationToken { get; set; }
}
