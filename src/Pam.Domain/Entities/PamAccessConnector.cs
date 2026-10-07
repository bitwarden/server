using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// An on-prem access connector registered to an organization. Connectivity is derived from
/// <see cref="LastHeartbeatAt"/>.
/// </summary>
public class PamAccessConnector : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>A Secrets Manager <c>dbo.ApiKey</c> row with a null <c>ServiceAccountId</c>.</summary>
    public Guid ApiKeyId { get; set; }

    public PamAccessConnectorStatus Status { get; set; }

    /// <summary>
    /// The last time the access connector polled or reported, bumped at most once per <c>HeartbeatMinInterval</c>.
    /// Null until its first request; sweeps never bump it.
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
