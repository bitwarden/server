using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Sends PAM's access emails. Never throws, and sends nothing while <see cref="Bit.Core.FeatureFlagKeys.Pam" /> is off.
/// </summary>
public interface IAccessMailNotifier
{
    /// <summary>Sends to <paramref name="recipientUserId" />, or nothing if the user does not exist.</summary>
    Task SendToUserAsync<TView>(Guid recipientUserId, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView;

    /// <summary>
    /// Sends each recipient their own message, abandoning the rest if delivery is down or too slow.
    /// </summary>
    Task SendToUsersAsync<TView>(IEnumerable<Guid> recipientUserIds, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView;
}
