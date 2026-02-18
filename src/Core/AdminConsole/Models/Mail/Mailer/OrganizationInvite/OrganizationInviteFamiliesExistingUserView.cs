using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public class OrganizationInviteFamiliesExistingUserView : OrganizationInviteBaseView
{
}

public class OrganizationInviteFamiliesExistingUser : BaseMail<OrganizationInviteFamiliesExistingUserView>
{
    public override required string Subject { get; set; }
}
