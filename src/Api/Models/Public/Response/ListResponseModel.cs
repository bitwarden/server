using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Public.Response;

public class ListResponseModel<T> : IResponseModel
    where T : IResponseModel
{
    public ListResponseModel(IEnumerable<T> data, string continuationToken = null)
    {
        Data = data;
        ContinuationToken = continuationToken;
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>list</example>
    [Required]
    public string Object => "list";

    /// <summary>
    /// An array containing the actual response elements, paginated by any request parameters.
    /// </summary>
    [Required]
    public IEnumerable<T> Data { get; set; }

    /// <summary>
    /// A cursor for use in pagination.
    /// </summary>
    public string ContinuationToken { get; set; }
}
