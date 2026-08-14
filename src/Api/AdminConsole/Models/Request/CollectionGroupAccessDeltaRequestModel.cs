using Bit.Api.Models.Request;

namespace Bit.Api.AdminConsole.Models.Request;

/// <summary>
/// Explicit add/update/remove changes to a collection's group access, rather than the full desired list.
/// </summary>
public class CollectionGroupAccessDeltaRequestModel
{
    public IEnumerable<SelectionReadOnlyRequestModel> Add { get; set; } = [];
    public IEnumerable<SelectionReadOnlyRequestModel> Update { get; set; } = [];
    public IEnumerable<Guid> Remove { get; set; } = [];
}
