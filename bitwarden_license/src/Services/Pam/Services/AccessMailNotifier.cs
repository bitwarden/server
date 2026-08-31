using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

public class AccessMailNotifier : IAccessMailNotifier
{
    private readonly IMailer _mailer;
    private readonly IUserRepository _userRepository;
    private readonly IFeatureService _featureService;
    private readonly ILogger<AccessMailNotifier> _logger;

    public AccessMailNotifier(
        IMailer mailer,
        IUserRepository userRepository,
        IFeatureService featureService,
        ILogger<AccessMailNotifier> logger)
    {
        _mailer = mailer;
        _userRepository = userRepository;
        _featureService = featureService;
        _logger = logger;
    }

    private bool Enabled => _featureService.IsEnabled(FeatureFlagKeys.Pam);

    public async Task SendToUserAsync<TView>(Guid recipientUserId, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            var recipient = await _userRepository.GetByIdAsync(recipientUserId);
            await SendOneAsync(recipientUserId, recipient?.Email, buildMail);
        }
        catch (Exception ex)
        {
            LogFailure(ex, recipientUserId);
        }
    }

    public async Task SendToUsersAsync<TView>(
        IEnumerable<Guid> recipientUserIds,
        Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView
    {
        if (!Enabled)
        {
            return;
        }

        var userIds = recipientUserIds.Distinct().ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        List<User> recipients;
        try
        {
            recipients = (await _userRepository.GetManyAsync(userIds)).ToList();
        }
        catch (Exception ex)
        {
            // The read covers every recipient, so its failure is the whole batch's failure and there is no
            // per-recipient id worth naming.
            _logger.LogError(ex, "PAM access mail: failed to resolve {RecipientCount} recipients.", userIds.Count);
            return;
        }

        foreach (var recipient in recipients)
        {
            try
            {
                await SendOneAsync(recipient.Id, recipient.Email, buildMail);
            }
            catch (Exception ex)
            {
                LogFailure(ex, recipient.Id);
            }
        }
    }

    private async Task SendOneAsync<TView>(Guid userId, string? email, Func<string, BaseMail<TView>> buildMail)
        where TView : BaseMailView
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("PAM access mail: no deliverable address for user {UserId}; nothing sent.", userId);
            return;
        }

        await _mailer.SendEmail(buildMail(email));
    }

    private void LogFailure(Exception ex, Guid userId) =>
        // Ids only. The recipient's address is the one thing this type always holds and must never record.
        _logger.LogError(ex, "PAM access mail to user {UserId} could not be sent.", userId);
}
