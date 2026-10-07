using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A system PAM rotates credentials on, through a <see cref="PamAccessConnector"/> or by a human out of band.
/// </summary>
public class PamTargetSystem : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    public PamTargetSystemMethod Method { get; set; }

    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// A serialized <see cref="Models.PamPasswordPolicy"/>. Required on an automatic target, optional on a
    /// <see cref="PamTargetSystemMethod.Manual"/> one.
    /// </summary>
    [MaxLength(2000)]
    public string? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the target can terminate an account's sessions after a rotation; null on a manual target. Must be
    /// true for a config to set <see cref="PamRotationConfig.TerminateSessions"/>.
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
