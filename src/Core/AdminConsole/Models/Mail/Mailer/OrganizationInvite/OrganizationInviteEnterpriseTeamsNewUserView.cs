using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public class OrganizationInviteEnterpriseTeamsNewUserView : OrganizationInviteBaseView
{
}

public class OrganizationInviteEnterpriseTeamsNewUser : BaseMail<OrganizationInviteEnterpriseTeamsNewUserView>
{
    public override required string Subject { get; set; }
}
