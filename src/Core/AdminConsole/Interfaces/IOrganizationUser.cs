using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Interfaces;

public interface IOrganizationUser
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public OrganizationUserType Type { get; set; }
    public OrganizationUserStatusType Status { get; set; }
}
