// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Data;

public class PagedResult<T>
{
    public List<T> Data { get; set; } = new List<T>();
    public string ContinuationToken { get; set; }
}
