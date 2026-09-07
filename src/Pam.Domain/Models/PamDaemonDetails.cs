using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamDaemon"/> together with its owning organization's licensing state, loaded by
/// <c>PamDaemonClientProvider</c> on every token request to decide whether the daemon may authenticate.
/// </summary>
public class PamDaemonDetails : PamDaemon
{
    public bool OrganizationEnabled { get; set; }
    public bool OrganizationUsePam { get; set; }

    public static PamDaemonDetails From(PamDaemon daemon, bool organizationEnabled, bool organizationUsePam) => new()
    {
        Id = daemon.Id,
        OrganizationId = daemon.OrganizationId,
        Name = daemon.Name,
        ApiKeyId = daemon.ApiKeyId,
        Status = daemon.Status,
        LastHeartbeatAt = daemon.LastHeartbeatAt,
        CreationDate = daemon.CreationDate,
        RevisionDate = daemon.RevisionDate,
        OrganizationEnabled = organizationEnabled,
        OrganizationUsePam = organizationUsePam,
    };
}
