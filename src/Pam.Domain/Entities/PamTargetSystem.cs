using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A system PAM can rotate credentials against: an <see cref="PamTargetSystemMethod.Automatic"/> target driven by a
/// <see cref="PamDaemon"/> (Entra, MSSQL, or a custom script), or a <see cref="PamTargetSystemMethod.Manual"/> target
/// that only tracks a schedule and records rotations a human performs out of band. <see cref="Kind"/> and
/// <see cref="PasswordPolicy"/> are set only for an automatic target.
/// </summary>
public class PamTargetSystem : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    public PamTargetSystemMethod Method { get; set; }

    /// <summary>The automatic connector kind. Null for a <see cref="PamTargetSystemMethod.Manual"/> target.</summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// JSON document of the <see cref="Models.PamPasswordPolicy"/> the rotation daemon generates candidate passwords
    /// against. Null for a <see cref="PamTargetSystemMethod.Manual"/> target.
    /// </summary>
    [MaxLength(2000)]
    public string? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the target can terminate an account's existing sessions after a rotation. Null until a daemon
    /// reports its capability; a <see cref="PamRotationConfig"/> may only set
    /// <see cref="PamRotationConfig.TerminateSessions"/> once this is true.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }

    public PamTargetSystemStatus Status { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
