using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public class OrganizationInviteFamiliesNewUserView : OrganizationInviteBaseView
{
}

public class OrganizationInviteFamiliesNewUser : BaseMail<OrganizationInviteFamiliesNewUserView>
{
    public override required string Subject { get; set; }
}
