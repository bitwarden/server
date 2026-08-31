using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// One offer of rotation work for a config, together with every attempt recorded against it -- an element of
/// <see cref="PamRotationConfigDetailResponseModel.Jobs"/> (the config detail page's attempt history).
/// </summary>
public class PamRotationJobResponseModel
{
    /// <summary>
    /// The job's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The rotation config this job was offered for.
    /// </summary>
    public Guid RotationConfigId { get; set; }

    /// <summary>
    /// What caused the job to be offered -- see <see cref="PamRotationSource"/>.
    /// </summary>
    public PamRotationSource Source { get; set; }

    /// <summary>
    /// Where the job stands -- see <see cref="PamRotationJobStatus"/>. A config has at most one job in an active
    /// status at a time.
    /// </summary>
    public PamRotationJobStatus Status { get; set; }

    /// <summary>
    /// The access connector currently holding the job's claim. Null unless <see cref="Status"/> is
    /// <see cref="PamRotationJobStatus.Claimed"/>.
    /// </summary>
    public Guid? ClaimedByAccessConnectorId { get; set; }

    /// <summary>
    /// When the current claim was taken (UTC). Null unless <see cref="Status"/> is
    /// <see cref="PamRotationJobStatus.Claimed"/>.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>
    /// When the job was offered (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// The earliest time the job can be claimed (UTC). Pushed out on retry (backoff) or release.
    /// </summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>
    /// When the job times out if no attempt has succeeded by then (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Every attempt recorded against this job, oldest first.
    /// </summary>
    public IReadOnlyList<PamRotationAttemptResponseModel> Attempts { get; set; } = [];
}
