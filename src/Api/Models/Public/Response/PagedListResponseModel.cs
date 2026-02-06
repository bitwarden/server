namespace Bit.Api.Models.Public.Response;

public class PagedListResponseModel<T>(IEnumerable<T> data, string continuationToken) : ListResponseModel<T>(data)
    where T : IResponseModel
{
    /// <summary>
    /// A cursor for use in pagination.
    /// </summary>
    public string ContinuationToken { get; set; } = continuationToken;
}
