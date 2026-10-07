using Bit.Core;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

public class AccessMailNotifier : IAccessMailNotifier
{
    // The calling command awaits this batch, and each failed send costs a retry delay. One failure may be a bad
    // address; two in a row suggest an outage.
    private const int ConsecutiveFailureLimit = 2;

    // SendGrid swallows a failed send after retrying, so only elapsed time reveals an outage there. Far above a
    // healthy batch's cost, since the recipient set is unbounded and a large, slow batch must still finish.
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
            // Projected before the first send, so the rows' decrypted master-password hashes and user keys are not
            // held for the whole batch.
            recipients = (await _userRepository.GetManyAsync(userIds))
                .Select(user => new Recipient(user.Id, user.Email))
                .ToList();
        }
        catch (Exception ex)
        {
            // One read covers every recipient, so there is no single user id to log.
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
        // Ids only; the recipient's address must never be logged.
        _logger.LogError(ex, "PAM access mail to user {UserId} could not be sent.", userId);

    private sealed record Recipient(Guid Id, string? Email);
}
