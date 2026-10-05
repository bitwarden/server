using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamAccessConnector"/> together with its owning organization's licensing state, loaded by
/// <c>PamAccessConnectorClientProvider</c> on every token request to decide whether the access connector may
/// authenticate.
/// </summary>
public class PamAccessConnectorDetails : PamAccessConnector
{
    public bool OrganizationEnabled { get; set; }
    public bool OrganizationUsePam { get; set; }

    public static PamAccessConnectorDetails From(
        PamAccessConnector accessConnector, bool organizationEnabled, bool organizationUsePam) => new()
        {
            Id = accessConnector.Id,
            OrganizationId = accessConnector.OrganizationId,
            Name = accessConnector.Name,
            ApiKeyId = accessConnector.ApiKeyId,
            Status = accessConnector.Status,
            LastHeartbeatAt = accessConnector.LastHeartbeatAt,
            CreationDate = accessConnector.CreationDate,
            RevisionDate = accessConnector.RevisionDate,
            OrganizationEnabled = organizationEnabled,
            OrganizationUsePam = organizationUsePam,
        };
}
