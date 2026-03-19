using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;

public class EmergencyAccessRemoveGranteesMailView : BaseMailView
{

    public required IEnumerable<string> RemovedGranteeEmails { get; set; }
    // ReSharper disable once MemberCanBeMadeStatic.Global
#pragma warning disable CA1822
    // Handlebars needs it to be an instance variable to work properly.
    public string EmergencyAccessHelpPageUrl => "https://bitwarden.com/help/emergency-access/";
#pragma warning restore CA1822
}

public class EmergencyAccessRemoveGranteesMail : BaseMail<EmergencyAccessRemoveGranteesMailView>
{
    public override string Subject { get; set; } = "Emergency contacts removed";
}
