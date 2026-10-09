using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessLeaseRevoked;

/// <summary>
/// Tells the holder an operator revoked their lease. Names no collection or cipher and omits the reason.
/// </summary>
public class AccessLeaseRevokedView : PamAccessMailView
{
    /// <summary>When the lease would have ended, in UTC.</summary>
    public required DateTime NotAfter { get; init; }

    public string ScheduledEnd => FormatWindow(NotAfter);
}

public class AccessLeaseRevokedMail : BaseMail<AccessLeaseRevokedView>
{
    public override string Subject { get; set; } = "Your access was revoked";
}
