using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public class OrganizationInviteEnterpriseTeamsExistingUserView : OrganizationInviteBaseView
{
}

public class OrganizationInviteEnterpriseTeamsExistingUser : BaseMail<OrganizationInviteEnterpriseTeamsExistingUserView>
{
    public override required string Subject { get; set; }
}
