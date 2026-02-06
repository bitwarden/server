// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class UpdateGroupIdsRequestModel
{
    /// <summary>
    /// The associated group ids that this object can access.
    /// </summary>
    public IEnumerable<Guid> GroupIds { get; set; }
}
