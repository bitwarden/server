using Bit.Core;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

public class AccessMailNotifier : IAccessMailNotifier
{
    // Every send costs a retry delay while delivery is down, and the access-request command awaits this batch. One
    // failure is as likely to be one unusable address as an outage; two in a row is not.
    private const int ConsecutiveFailureLimit = 2;

    // The SendGrid path swallows a failed send once it has retried, so it never reaches the count above and only
    // elapsed time reveals it. Deliberately far above what a healthy batch costs, since the managing-user set this
    // sends to has no upper bound: a large one that is merely slow must finish, not be silently cut short.
    private static readonly TimeSpan _batchBudget = TimeSpan.FromSeconds(30);

    private readonly IMailer _mailer;
    private readonly IUserRepository _userRepository;
    private readonly IFeatureService _featureService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccessMailNotifier> _logger;

    public AccessMailNotifier(
        IMailer mailer,
        IUserRepository userRepository,
        IFeatureService featureService,
        TimeProvider timeProvider,
        ILogger<AccessMailNotifier> logger)
    {
        _mailer = mailer ?? throw new ArgumentNullException(nameof(mailer));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        List<Recipient> recipients;
        try
        {
            // Projected before the first send: the rows carry a decrypted master-password hash and user key, and
            // holding the whole batch of them alive for the length of the batch is a needlessly long exposure.
            recipients = (await _userRepository.GetManyAsync(userIds))
                .Select(user => new Recipient(user.Id, user.Email))
                .ToList();
        }
        catch (Exception ex)
        {
            // The read covers every recipient, so its failure is the whole batch's failure and there is no
            // per-recipient id worth naming.
            _logger.LogError(ex, "PAM access mail: failed to resolve {RecipientCount} recipients.", userIds.Count);
            return;
        }

        var startedAt = _timeProvider.GetTimestamp();
        var consecutiveFailures = 0;

        for (var i = 0; i < recipients.Count; i++)
        {
            if (consecutiveFailures >= ConsecutiveFailureLimit)
            {
                _logger.LogError(
                    "PAM access mail: {FailureCount} consecutive delivery failures; {SkippedCount} of {RecipientCount} recipients were not attempted.",
                    consecutiveFailures, recipients.Count - i, recipients.Count);
                return;
            }

            if (_timeProvider.GetElapsedTime(startedAt) >= _batchBudget)
            {
                _logger.LogError(
                    "PAM access mail: the {BudgetSeconds}s batch budget was spent; {SkippedCount} of {RecipientCount} recipients were not attempted.",
                    _batchBudget.TotalSeconds, recipients.Count - i, recipients.Count);
                return;
            }

            var recipient = recipients[i];
            try
            {
                await SendOneAsync(recipient.Id, recipient.Email, buildMail);
                consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
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

    private sealed record Recipient(Guid Id, string? Email);
}
