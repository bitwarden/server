using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestsWaiting;

/// <summary>
/// The single message an approver gets in place of the rest of a burst, once
/// <c>ApproverMailNotifier</c>'s circuit breaker has tripped for them. It names no individual request
/// because the requests it stands in for are still arriving; the approver inbox at <see cref="Url" /> is the
/// only live count.
/// </summary>
/// <remarks>
/// It names no organization either. The breaker counts per approver across every organization they manage in, so
/// attributing the count to the organization of the request that happened to trip it would misstate where the
/// backlog is. <see cref="Url" /> is not organization-scoped and shows the true set.
/// </remarks>
public class AccessRequestsWaitingView : BaseMailView
{
    public required string WebVaultUrl { get; init; }

    /// <summary>How many requests reached this approver inside the current window, including the one that tripped the breaker.</summary>
    public required int RequestCount { get; init; }

    /// <summary>
    /// The length of that window. Carried on the view rather than written into the copy so the sentence cannot
    /// drift from <c>ApproverMailNotifier.BurstWindowMinutes</c>.
    /// </summary>
    public required int WindowMinutes { get; init; }

    /// <summary>
    /// The approver inbox. Same route tree as <c>AccessRequestPendingView.Url</c>: the user-scoped
    /// <c>pam</c> mount (<c>apps/web/src/app/oss-routing.module.ts:687</c>) plus
    /// <c>approvals</c> (<c>access-requests-routing.module.ts:25</c>).
    /// </summary>
    public string Url => $"{WebVaultUrl}/pam/approvals";
}

public class AccessRequestsWaitingMail : BaseMail<AccessRequestsWaitingView>
{
    public override string Subject { get; set; } = "Access requests are waiting for your decision";
}
