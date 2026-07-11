using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IReportRotationFailedCommand
{
    /// <summary>
    /// Records a failed rotation attempt (spec <c>RecordRotationFailed</c> → <c>RetryJob</c> / <c>FailJob</c>).
    /// <paramref name="failureReason"/> is truncated to 500 characters before anything else happens — never
    /// rejected — since the contract forbids forwarding raw target-system error output (it can echo credentials).
    /// Retries the job while the retry budget (<c>MaxAttempts</c>) remains, otherwise fails it outright and pushes
    /// the config's next rotation out by <c>FailureRetryDelay</c>. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt id (no audit) and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> for a stale report (spec
    /// <c>RejectStaleFailureReport</c> — audited as <c>report_rejected</c>).
    /// </summary>
    Task<PamRotationAttempt> ReportFailedAsync(
        Guid daemonId, Guid attemptId, string? failureReason, PamRotationSyncState syncState);
}
