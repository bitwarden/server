using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// A job the release sweep returned to <see cref="PamRotationJobStatus.Pending"/>, with what its audit event needs,
/// since the same update clears the job's claim fields.
/// </summary>
public record PamReleasedJob
{
    public required Guid JobId { get; init; }
    public required Guid RotationConfigId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid CipherId { get; init; }
    public required PamRotationSource Source { get; init; }

    public required Guid ClaimedByAccessConnectorId { get; init; }
}
