using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>POST rotation/attempts/{id}/failure</c> (spec <c>RecordRotationFailed</c>). The contract forbids
/// forwarding raw target-system error output -- it can echo credentials -- so the daemon reports a bounded
/// <see cref="ErrorCode"/> (an enum-ish string token it defines) plus an optional, separately-capped
/// <see cref="Detail"/>; the server truncates the combined reason to 500 characters regardless (never rejected).
/// </summary>
public class ReportRotationFailedRequestModel
{
    /// <summary>
    /// Whether the failure left the target system's password changed -- that is, whether the vault credential
    /// still matches the target. Recorded on the attempt so an operator can tell a clean failure from
    /// credential drift.
    /// </summary>
    [Required]
    public PamRotationSyncState SyncState { get; set; }

    /// <summary>
    /// A bounded, daemon-defined token classifying the failure -- never raw target-system output.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ErrorCode { get; set; } = null!;

    /// <summary>
    /// Additional human-readable context for the failure, subject to the same no-raw-target-output contract as
    /// <see cref="ErrorCode"/>.
    /// </summary>
    [StringLength(500)]
    public string? Detail { get; set; }

    /// <summary>Combines <see cref="ErrorCode"/> and <see cref="Detail"/> into the single reason string the command records.</summary>
    public string ToFailureReason() => Detail is null ? ErrorCode : $"{ErrorCode}: {Detail}";
}
