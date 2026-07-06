using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IReportRotationSucceededCommand
{
    /// <summary>
    /// Records a successful rotation attempt (spec <c>RecordRotationSucceeded</c> → <c>MarkJobSucceeded</c>).
    /// Requires the attempt to already have a written cipher (<c>CipherUpdated</c> — the <c>VerifiedBeforeSuccess</c>
    /// backstop). Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> for an unknown attempt id (no audit —
    /// nothing to audit against) and <see cref="Bit.Core.Exceptions.ConflictException"/> for a stale report (spec
    /// <c>RejectStaleSuccess</c> — audited as <c>report_rejected</c>).
    /// </summary>
    Task<PamRotationAttempt> ReportSucceededAsync(
        Guid daemonId, Guid attemptId, PamSessionTerminationOutcome sessionTermination);
}
