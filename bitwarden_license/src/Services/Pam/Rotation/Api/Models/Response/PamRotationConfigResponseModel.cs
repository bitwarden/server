using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A rotation config's schedule-list view (spec derived predicates <c>has_active_job</c> /
/// <c>awaiting_manual_rotation</c> folded in) -- the view model for <c>GET rotation/configs</c> and the summary
/// embedded in <see cref="PamRotationConfigDetailResponseModel"/>.
/// </summary>
public class PamRotationConfigResponseModel : ResponseModel
{
    public PamRotationConfigResponseModel(PamRotationConfigDetails config, bool awaitingManualRotation)
        : base("pamRotationConfig")
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = config.Id;
        OrganizationId = config.OrganizationId;
        CipherId = config.CipherId;
        TargetSystemId = config.TargetSystemId;
        TargetSystemName = config.TargetSystemName;
        TargetSystemMethod = config.TargetSystemMethod;
        AccountIdentity = config.AccountIdentity;
        TerminateSessions = config.TerminateSessions;
        ScheduleCron = config.ScheduleCron;
        RotateOnAccessEnd = config.RotateOnAccessEnd;
        NextRotationAt = config.NextRotationAt.AsUtc();
        Enabled = config.Enabled;
        LastRotationAt = config.LastRotationAt.AsUtc();
        HasActiveJob = config.HasActiveJob;
        AwaitingManualRotation = awaitingManualRotation;
        CreationDate = config.CreationDate.AsUtc();
        RevisionDate = config.RevisionDate.AsUtc();
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid CipherId { get; }
    public Guid TargetSystemId { get; }
    public string TargetSystemName { get; }
    public PamTargetSystemMethod TargetSystemMethod { get; }

    /// <summary>Opaque to the server -- never parsed; only the daemon interprets it.</summary>
    public string AccountIdentity { get; }

    public bool TerminateSessions { get; }
    public string? ScheduleCron { get; }
    public bool RotateOnAccessEnd { get; }
    public DateTime? NextRotationAt { get; }
    public bool Enabled { get; }
    public DateTime? LastRotationAt { get; }

    /// <summary>Whether the config has a Pending or Claimed job -- spec <c>has_active_job</c>.</summary>
    public bool HasActiveJob { get; }

    /// <summary>
    /// A manual-target config whose schedule has come due -- spec <c>awaiting_manual_rotation</c>. There is no job
    /// to claim; an operator must record the out-of-band rotation via <c>POST configs/{id}/record-manual</c>.
    /// </summary>
    public bool AwaitingManualRotation { get; }

    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }
}
