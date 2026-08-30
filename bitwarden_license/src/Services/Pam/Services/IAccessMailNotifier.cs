using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Sends PAM's access-lifecycle emails — the out-of-band half of the notification story that
/// <see cref="IApproverInboxNotifier" /> and <see cref="IRequesterNotifier" /> cover in-band. Their pushes only
/// reach a client that happens to be open, so an approver with nothing running never learns a request is waiting;
/// this delivers the same news to a mailbox. Fired from the access-request and lease commands alongside the
/// matching push.
/// </summary>
/// <remarks>
/// Two contracts bind every implementation, and both exist because a send sits inside a command that grants or
/// ends access to a vault item.
///
/// It never throws. <c>Mailer.SendEmail</c> reaches <c>IMailDeliveryService</c> with no queue in between, so a
/// propagating failure would fail the enclosing access-request command: an upstream mail outage would stop people
/// getting access to their own items. A failed send is logged and swallowed; the request still succeeds and the
/// push still lands.
///
/// It sends nothing while <see cref="Bit.Core.FeatureFlagKeys.PamEmailNotifications" /> is off, which is the
/// absent-flag default and the only state self-host sees.
/// </remarks>
public interface IAccessMailNotifier
{
    /// <summary>
    /// Resolves <paramref name="recipientUserId" />'s address and sends the mail <paramref name="buildMail" />
    /// returns for it. The address is supplied to the factory rather than known to the caller, so a caller never
    /// has to load a user to send them mail.
    /// </summary>
    /// <param name="recipientUserId">The user to deliver to. Nothing is sent if they no longer exist.</param>
    /// <param name="buildMail">Builds the mail for one resolved address; called at most once.</param>
    Task SendToUserAsync<TView>(Guid recipientUserId, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView;

    /// <summary>
    /// The <see cref="SendToUserAsync{TView}" /> contract for several recipients, resolved in one read. Each gets
    /// their own message: a shared <c>ToEmails</c> would disclose an organization's approvers to one another.
    /// One recipient failing does not stop the rest.
    /// </summary>
    Task SendToUsersAsync<TView>(IEnumerable<Guid> recipientUserIds, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView;
}
