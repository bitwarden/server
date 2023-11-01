namespace Bit.Api.Models.Request;

public class BulkCollectionAccessRequestModel
{
    public IEnumerable<Guid> CollectionIds { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Users { get; set; }
}
