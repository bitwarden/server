using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestPending;

/// <summary>
/// Tells an approver a request awaits their decision. Names no collection or cipher and omits the reason.
/// </summary>
public class AccessRequestPendingView : PamAccessMailView
{
    public required string RequesterEmail { get; init; }

    public required DateTime NotBefore { get; init; }

    public required DateTime NotAfter { get; init; }

    public string WindowStart => FormatWindow(NotBefore);

    public string WindowEnd => FormatWindow(NotAfter);
}

public class AccessRequestPendingMail : BaseMail<AccessRequestPendingView>
{
    public override string Subject { get; set; } = "An access request is waiting for your decision";
}
