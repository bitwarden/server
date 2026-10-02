using Bit.Core;
using Bit.Core.Auth.Repositories;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Admin.Auth.Jobs;

/// <summary>
/// Clears out remember-token rows whose expiry has passed. Hygiene rather than correctness — an
/// expired row is refused at validation time regardless, the row count is bounded by the number of
/// devices, and the expiry index makes the sweep cheap. Weekly is sufficient.
/// </summary>
public class DeleteExpiredTwoFactorRememberTokensJob : BaseJob
{
    private readonly ITwoFactorRememberTokenRepository _twoFactorRememberTokenRepository;
    private readonly TimeProvider _timeProvider;

    public DeleteExpiredTwoFactorRememberTokensJob(
        ITwoFactorRememberTokenRepository twoFactorRememberTokenRepository,
        TimeProvider timeProvider,
        ILogger<DeleteExpiredTwoFactorRememberTokensJob> logger)
        : base(logger)
    {
        _twoFactorRememberTokenRepository = twoFactorRememberTokenRepository;
        _timeProvider = timeProvider;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(
            Constants.BypassFiltersEventId, "Execute job task: DeleteExpiredTwoFactorRememberTokensAsync");
        await _twoFactorRememberTokenRepository.DeleteExpiredAsync(_timeProvider.GetUtcNow().UtcDateTime);
        _logger.LogInformation(
            Constants.BypassFiltersEventId, "Finished job task: DeleteExpiredTwoFactorRememberTokensAsync");
    }
}
