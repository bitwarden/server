using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestsWaiting;

/// <summary>
/// The single message an approver gets in place of the rest of a burst, once
/// <c>ApproverMailNotifier</c>'s circuit breaker has tripped for them. It names no individual request
/// because the requests it stands in for are still arriving; the approver inbox at <see cref="Url" /> is the
/// only live count.
/// </summary>
public class AccessRequestsWaitingView : BaseMailView
{
    public required string WebVaultUrl { get; init; }

    public required string OrganizationName { get; init; }

    /// <summary>How many requests reached this approver inside the current window, including the one that tripped the breaker.</summary>
    public required int RequestCount { get; init; }

    /// <summary>
    /// The length of that window. Carried on the view rather than written into the copy so the sentence cannot
    /// drift from <c>ApproverMailNotifier.BurstWindowMinutes</c>.
    /// </summary>
    public required int WindowMinutes { get; init; }

    /// <summary>
    /// The approver inbox. Same route tree as <c>AccessRequestPendingView.Url</c>: the user-scoped
    /// <c>privileged-controls</c> mount (<c>apps/web/src/app/oss-routing.module.ts:687</c>) plus
    /// <c>approvals</c> (<c>access-requests-routing.module.ts:25</c>).
    /// </summary>
    public string Url => $"{WebVaultUrl}/privileged-controls/approvals";
}

public class AccessRequestsWaitingMail : BaseMail<AccessRequestsWaitingView>
{
    public override string Subject { get; set; } = "Access requests are waiting for your decision";
}
