using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One job whose <see cref="PamRotationJobStatus.TimedOut"/> the sweep has just recorded: the row it needs to emit the
/// <c>timed_out</c> audit event with its unroutable-vs-stuck reason.
/// </summary>
public record PamTimedOutJob
{
    public required Guid JobId { get; init; }
    public required Guid RotationConfigId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid CipherId { get; init; }
    public required PamRotationSource Source { get; init; }

    /// <summary>The claim held at timeout; null when the job was Pending.</summary>
    public Guid? ClaimedByAccessConnectorId { get; init; }

    /// <summary>Zero means unroutable (never claimed); nonzero means stuck.</summary>
    public required int AttemptCount { get; init; }
}
