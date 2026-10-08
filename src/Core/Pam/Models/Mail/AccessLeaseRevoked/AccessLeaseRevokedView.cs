using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessLeaseRevoked;

/// <summary>
/// The holder's notice that an operator revoked their lease, a courtesy sent after access has ended. Like its siblings
/// it names no collection or cipher and withholds the reason, free text that could name the system being accessed.
/// </summary>
public class AccessLeaseRevokedView : PamAccessMailView
{
    /// <summary>
    /// When the lease would have ended, in UTC; always in the future at send time, since a closed lease cannot be
    /// revoked.
    /// </summary>
    public required DateTime NotAfter { get; init; }

    public string ScheduledEnd => FormatWindow(NotAfter);
}

public class AccessLeaseRevokedMail : BaseMail<AccessLeaseRevokedView>
{
    public override string Subject { get; set; } = "Your access was revoked";
}
