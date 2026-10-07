using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IReportRotationFailedCommand
{
    /// <summary>
    /// Records a failed attempt, retrying or failing its job. <paramref name="failureReason"/> is truncated, never
    /// rejected. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> for a stale report.
    /// </summary>
    Task<PamRotationAttempt> ReportFailedAsync(
        Guid accessConnectorId, Guid attemptId, string? failureReason, PamRotationSyncState syncState);
}
