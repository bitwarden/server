using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// An on-prem rotation daemon registered against an organization (spec <c>DaemonRegistration</c>). The daemon's
/// machine credential is a generic <c>dbo.ApiKey</c> row referenced by <see cref="ApiKeyId"/> — PAM reuses the
/// Secrets Manager credential store rather than minting a parallel one; the owner link is inverted relative to
/// <c>ApiKey.ServiceAccountId</c>. There is no persisted connection row: liveness is derived from
/// <see cref="LastHeartbeatAt"/> (see <c>PamRotationRules.IsConnected</c>).
/// </summary>
public class PamDaemon : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>The daemon's machine credential — a <c>dbo.ApiKey</c> row with a null <c>ServiceAccountId</c>.</summary>
    public Guid ApiKeyId { get; set; }

    public PamDaemonStatus Status { get; set; }

    /// <summary>
    /// The last time the daemon polled or reported, bumped at most once per <c>HeartbeatMinInterval</c>. Null until
    /// its first request. Never bumped by a sweep — only by the daemon's own requests.
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
