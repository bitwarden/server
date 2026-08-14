using Bit.Api.Models.Request;

namespace Bit.Api.AdminConsole.Models.Request;

/// <summary>
/// Explicit add/update/remove changes to a collection's user access, rather than the full desired list.
/// </summary>
public class CollectionUserAccessDeltaRequestModel
{
    public IEnumerable<SelectionReadOnlyRequestModel> Add { get; set; } = [];
    public IEnumerable<SelectionReadOnlyRequestModel> Update { get; set; } = [];
    public IEnumerable<Guid> Remove { get; set; } = [];
}
