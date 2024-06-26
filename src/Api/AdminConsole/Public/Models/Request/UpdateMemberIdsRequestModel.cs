namespace Bit.Api.AdminConsole.Public.Models.Request;

public class UpdateMemberIdsRequestModel
{
    /// <summary>
    /// The associated member ids that have access to this object.
    /// </summary>
    public IEnumerable<Guid> MemberIds { get; set; }
}
