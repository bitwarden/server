using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Sends PAM's access-lifecycle emails, never throwing: <c>Mailer.SendEmail</c> has no queue, so a mail outage would
/// otherwise fail the calling command. Sends nothing while <see cref="Bit.Core.FeatureFlagKeys.Pam" /> is off, as on
/// self-host.
/// </summary>
public interface IAccessMailNotifier
{
    /// <summary>
    /// Resolves <paramref name="recipientUserId" />'s address and hands it to <paramref name="buildMail" />. Nothing
    /// is sent if the user does not exist.
    /// </summary>
    Task SendToUserAsync<TView>(Guid recipientUserId, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView;

    /// <summary>
    /// <see cref="SendToUserAsync{TView}" /> for several recipients. Each gets their own message, so approvers are
    /// not disclosed to one another; a delivery path that is down or too slow abandons the rest.
    /// </summary>
    Task SendToUsersAsync<TView>(IEnumerable<Guid> recipientUserIds, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView;
}
