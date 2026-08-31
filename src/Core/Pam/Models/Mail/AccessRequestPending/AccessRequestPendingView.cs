using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestPending;

/// <summary>
/// One approver's notification that a named requester is waiting on their decision.
/// </summary>
/// <remarks>
/// What may appear here is bounded by zero knowledge, not by copywriting. The collection and the cipher are named
/// only by ciphertext the server cannot read, and <c>AccessRequest.Reason</c> is withheld: though stored in the
/// clear, it is user-typed free text that would name the very system being accessed. The body identifies the
/// request by the organization, the requester and the window, and sends the approver to
/// <see cref="PamAccessMailView.Url" /> for everything else.
/// </remarks>
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
