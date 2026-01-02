using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;

public class EmergencyAccessRemoveGranteesMailView : BaseMailView
{
    public required IEnumerable<string> RemovedGranteeNames { get; set; }
    public required string WebVaultUrl { get; set; }
}

public class EmergencyAccessRemoveGranteesMail : BaseMail<EmergencyAccessRemoveGranteesMailView>
{
    public override string Subject => "Emergency contacts removed";
}
