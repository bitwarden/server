using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;

public class EmergencyAccessRemoveGranteesMailView : BaseMailView
{

    public required IEnumerable<string> RemovedGranteeEmails { get; set; }
    public static string EmergencyAccessHelpPageUrl => "https://bitwarden.com/help/emergency-access/";
}

public class EmergencyAccessRemoveGranteesMail : BaseMail<EmergencyAccessRemoveGranteesMailView>
{
    public override string Subject { get; set; } = "Emergency contacts removed";
}
