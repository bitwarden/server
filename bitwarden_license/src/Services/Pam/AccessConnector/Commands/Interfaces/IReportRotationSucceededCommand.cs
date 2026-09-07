using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IReportRotationSucceededCommand
{
    /// <summary>
    /// Records a successful rotation attempt. Requires the attempt to already have a written cipher (the
    /// <c>VerifiedBeforeSuccess</c> backstop). Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> for an
    /// unknown attempt id, and <see cref="Bit.Core.Exceptions.ConflictException"/> for a stale report.
    /// </summary>
    Task<PamRotationAttempt> ReportSucceededAsync(
        Guid daemonId, Guid attemptId, PamSessionTerminationOutcome sessionTermination);
}
