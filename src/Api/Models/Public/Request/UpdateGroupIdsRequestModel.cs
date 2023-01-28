namespace Bit.Api.Models.Public.Request;

public class UpdateGroupIdsRequestModel
{
    /// <summary>
    /// The associated group ids that this object can access.
    /// </summary>
    public IEnumerable<Guid> GroupIds { get; set; }
}
