using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IReportRotationSucceededCommand
{
    /// <summary>
    /// Records a successful attempt, which must already have written the cipher (<c>VerifiedBeforeSuccess</c>).
    /// Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt and
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> for a stale report.
    /// </summary>
    Task<PamRotationAttempt> ReportSucceededAsync(
        Guid accessConnectorId, Guid attemptId, PamSessionTerminationOutcome sessionTermination);
}
