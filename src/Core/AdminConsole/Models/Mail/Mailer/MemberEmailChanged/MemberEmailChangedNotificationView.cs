using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.MemberEmailChanged;

public class MemberEmailChangedNotificationView : BaseMailView
{
    public required string NewEmail { get; set; }
}

public class MemberEmailChangedNotificationMail : BaseMail<MemberEmailChangedNotificationView>
{
    public override string Subject { get; set; } = "Your Bitwarden account email was updated";
}
