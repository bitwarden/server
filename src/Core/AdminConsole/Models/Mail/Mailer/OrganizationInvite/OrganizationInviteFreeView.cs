using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public class OrganizationInviteFreeView : OrganizationInviteBaseView
{
}

public class OrganizationInviteFree : BaseMail<OrganizationInviteFreeView>
{
    public override required string Subject { get; set; }
}
