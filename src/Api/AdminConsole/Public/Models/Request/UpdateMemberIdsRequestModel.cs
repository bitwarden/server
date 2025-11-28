// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class UpdateMemberIdsRequestModel
{
    /// <summary>
    /// The associated member ids that have access to this object.
    /// </summary>
    public IEnumerable<Guid> MemberIds { get; set; }
}
