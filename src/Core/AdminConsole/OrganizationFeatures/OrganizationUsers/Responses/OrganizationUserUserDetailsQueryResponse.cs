using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Responses;

public class OrganizationUserUserDetailsQueryResponse
{
    public OrganizationUserUserDetails OrganizationUserUserDetails { get; set; }
    public bool TwoFactorEnabled { get; set; }

    public OrganizationUserUserDetailsQueryResponse(
        OrganizationUserUserDetails organizationUserUserDetails,
        bool twoFactorEnabled
    )
    {
        this.OrganizationUserUserDetails = organizationUserUserDetails;
        this.TwoFactorEnabled = twoFactorEnabled;
    }
}
