// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.HttpExtensions;

/// <summary>
/// A paginated list response wrapper.
/// </summary>
public class ListResponseModel<T> : ResponseModel where T : ResponseModel
{
    public ListResponseModel(IEnumerable<T> data, string continuationToken = null)
        : base("list")
    {
        Data = data;
        ContinuationToken = continuationToken;
    }

    public IEnumerable<T> Data { get; set; }
    public string ContinuationToken { get; set; }
}
