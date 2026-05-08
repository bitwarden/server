namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// A join record linking an <see cref="Bit.Core.Entities.OrganizationUser"/> to a <see cref="Group"/>,
/// indicating that the organization user is a member of that group.
/// </summary>
public class GroupUser
{
    /// <summary>
    /// The ID of the <see cref="Group"/> the organization user belongs to.
    /// </summary>
    public Guid GroupId { get; set; }
    /// <summary>
    /// The ID of the <see cref="Bit.Core.Entities.OrganizationUser"/> that is a member of the group.
    /// </summary>
    public Guid OrganizationUserId { get; set; }
}
