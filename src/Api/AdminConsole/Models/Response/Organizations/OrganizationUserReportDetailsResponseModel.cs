using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationUserReportDetailsResponseModel : ResponseModel
{
    public OrganizationUserReportDetailsResponseModel(
        (OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization) data)
        : base("organizationUserReportDetails")
    {
        Id = data.OrgUser.Id;
        UserId = data.OrgUser.UserId;
        Name = data.OrgUser.Name;
        Email = data.OrgUser.Email;
        AvatarColor = data.OrgUser.AvatarColor;
        TwoFactorEnabled = data.TwoFactorEnabled;
        UsesKeyConnector = data.OrgUser.UsesKeyConnector;
        // Prevent reset password when using key connector.
        ResetPasswordEnrolled = OrganizationUser.IsValidResetPasswordKey(data.OrgUser.ResetPasswordKey)
            && !data.OrgUser.UsesKeyConnector;
        Groups = data.OrgUser.Groups;
    }

    public Guid Id { get; }
    public Guid? UserId { get; }
    public string? Name { get; }
    public string Email { get; }
    public string? AvatarColor { get; }
    public bool TwoFactorEnabled { get; }
    public bool UsesKeyConnector { get; }
    public bool ResetPasswordEnrolled { get; }
    public IEnumerable<Guid> Groups { get; }
}
