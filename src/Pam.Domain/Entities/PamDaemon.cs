using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// An on-prem rotation daemon registered against an organization (spec <c>DaemonRegistration</c>). Its machine
/// credential reuses the Secrets Manager <c>dbo.ApiKey</c> store via <see cref="ApiKeyId"/>, with the owner link
/// inverted relative to <c>ApiKey.ServiceAccountId</c>. Liveness is derived from <see cref="LastHeartbeatAt"/>,
/// not a persisted connection row.
/// </summary>
public class PamDaemon : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>The daemon's machine credential — a <c>dbo.ApiKey</c> row with a null <c>ServiceAccountId</c>.</summary>
    public Guid ApiKeyId { get; set; }

    public PamAccessConnectorStatus Status { get; set; }

    /// <summary>
    /// The last time the daemon polled or reported, bumped at most once per <c>HeartbeatMinInterval</c>. Null
    /// until its first request; never bumped by a sweep.
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
