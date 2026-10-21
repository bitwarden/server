using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// A failed rotation report (spec <c>RecordRotationFailed</c>). Raw target-system output can echo credentials, so the
/// access connector sends an <see cref="ErrorCode"/> and optional <see cref="Detail"/> instead, whose combination is
/// truncated rather than rejected.
/// </summary>
public class ReportRotationFailedRequestModel
{
    /// <summary>
    /// Whether the failure left the target system's password changed, so the vault credential no longer matches.
    /// Nullable so an omitted value is rejected rather than read as
    /// <see cref="PamRotationSyncState.TargetUnchanged"/>.
    /// </summary>
    [Required]
    [EnumDataType(typeof(PamRotationSyncState))]
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>A connector-defined token classifying the failure, never raw target-system output.</summary>
    [Required]
    [StringLength(100)]
    public string ErrorCode { get; set; } = null!;

    /// <summary>Human-readable context, under the same no-raw-output contract as <see cref="ErrorCode"/>.</summary>
    [StringLength(500)]
    public string? Detail { get; set; }

    public string ToFailureReason() => Detail is null ? ErrorCode : $"{ErrorCode}: {Detail}";
}
