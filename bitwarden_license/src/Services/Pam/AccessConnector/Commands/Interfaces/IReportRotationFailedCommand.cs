using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IReportRotationFailedCommand
{
    /// <summary>
    /// Records a failed rotation attempt. <paramref name="failureReason"/> is truncated to 500 characters, never
    /// rejected, since raw target-system error output can echo credentials. Retries the job while the retry
    /// budget remains, otherwise fails it and pushes the config's next rotation out by
    /// <c>FailureRetryDelay</c>. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown
    /// attempt id and <see cref="Bit.Core.Exceptions.ConflictException"/> for a stale report.
    /// </summary>
    Task<PamRotationAttempt> ReportFailedAsync(
        Guid daemonId, Guid attemptId, string? failureReason, PamRotationSyncState syncState);
}
