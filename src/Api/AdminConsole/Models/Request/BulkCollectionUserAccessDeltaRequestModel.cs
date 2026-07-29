using Bit.Api.Models.Request;

namespace Bit.Api.AdminConsole.Models.Request;

/// <summary>
/// A <see cref="CollectionUserAccessDeltaRequestModel"/> applied to every listed collection.
/// </summary>
public class BulkCollectionUserAccessDeltaRequestModel
{
    public IEnumerable<Guid> CollectionIds { get; set; } = [];
    public IEnumerable<SelectionReadOnlyRequestModel> Add { get; set; } = [];
    public IEnumerable<SelectionReadOnlyRequestModel> Update { get; set; } = [];
    public IEnumerable<Guid> Remove { get; set; } = [];
}
