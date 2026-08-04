using Bit.Api.Models.Request;

namespace Bit.Api.AdminConsole.Models.Request;

/// <summary>
/// Explicit add/update/remove changes to a collection's group access, rather than the full desired list.
/// </summary>
public class CollectionGroupAccessDeltaRequestModel
{
    public IEnumerable<SelectionReadOnlyRequestModel> Add { get; init; } = [];
    public IEnumerable<SelectionReadOnlyRequestModel> Update { get; init; } = [];
    public IEnumerable<Guid> Remove { get; init; } = [];
}
