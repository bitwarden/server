using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>POST rotation/attempts/{id}/failure</c> (spec <c>RecordRotationFailed</c>). The contract forbids
/// forwarding raw target-system error output -- it can echo credentials -- so the daemon reports a bounded
/// <see cref="ErrorCode"/> (an enum-ish string token it defines) plus an optional, separately-capped
/// <see cref="Detail"/>; <see cref="Bit.Services.Pam.Rotation.Commands.Interfaces.IReportRotationFailedCommand"/>
/// truncates the combined reason to 500 characters server-side regardless (never rejected).
/// </summary>
public class ReportRotationFailedRequestModel
{
    [Required]
    public PamRotationSyncState SyncState { get; set; }

    [Required]
    [StringLength(100)]
    public string ErrorCode { get; set; } = null!;

    [StringLength(500)]
    public string? Detail { get; set; }

    /// <summary>Combines <see cref="ErrorCode"/> and <see cref="Detail"/> into the single reason string the command records.</summary>
    public string ToFailureReason() => Detail is null ? ErrorCode : $"{ErrorCode}: {Detail}";
}
