using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One job the release sweep returned to <see cref="PamRotationJobStatus.Pending"/>. Carries the fields the sweep
/// needs to emit the <c>released</c> audit event, since the job's own claim fields are cleared by the same update.
/// </summary>
public record PamReleasedJob
{
    public required Guid JobId { get; init; }
    public required Guid RotationConfigId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid CipherId { get; init; }
    public required PamRotationSource Source { get; init; }

    /// <summary>The daemon whose claim was released — the job's pre-clear <c>ClaimedByDaemonId</c>.</summary>
    public required Guid ClaimedByDaemonId { get; init; }
}
