using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

/// <summary>
/// The member details used by organization reports (Member Access Report and Access Intelligence). It is returned to
/// users who can access reports but may not manage users, so it deliberately omits the permissions, collections, SSO
/// and other management data exposed by <see cref="OrganizationUserUserDetailsResponseModel"/>. Only add a field here
/// if a report needs it.
/// </summary>
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
