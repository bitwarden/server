using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Sends PAM's access-lifecycle emails: the out-of-band counterpart to the in-band pushes from
/// <see cref="IApproverInboxNotifier" /> and <see cref="IRequesterNotifier" />, which only reach a client that
/// happens to be open.
/// </summary>
/// <remarks>
/// Implementations never throw. <c>Mailer.SendEmail</c> reaches <c>IMailDeliveryService</c> with no queue in
/// between, so a propagating failure would fail the enclosing access-request command and a mail outage would stop
/// people getting access to their own items. A failed send is logged and swallowed.
///
/// Nothing is sent while <see cref="Bit.Core.FeatureFlagKeys.Pam" /> is off, which is the absent-flag default and
/// the only state self-host sees.
/// </remarks>
public interface IAccessMailNotifier
{
    /// <summary>
    /// Resolves <paramref name="recipientUserId" />'s address and hands it to <paramref name="buildMail" />, so a
    /// caller never has to load a user to send them mail. Nothing is sent if the user no longer exists.
    /// </summary>
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
