#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserUserMiniDetails
{
    public OrganizationUserUserMiniDetails(OrganizationUserUserDetails organizationUser)
    {
        Id = organizationUser.Id;
        UserId = organizationUser.UserId;
        Type = organizationUser.Type;
        Status = organizationUser.Status;
        Name = organizationUser.Name;
        Email = organizationUser.Email;
    }

    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public OrganizationUserType Type { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; }
}
