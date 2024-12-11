using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface ISelfHostedSyncSponsorshipsCommand
{
    Task SyncOrganization(
        Guid organizationId,
        Guid cloudOrganizationId,
        OrganizationConnection billingSyncConnection
    );
}

public interface ICloudSyncSponsorshipsCommand
{
    Task<(OrganizationSponsorshipSyncData, IEnumerable<OrganizationSponsorship>)> SyncOrganization(
        Organization sponsoringOrg,
        IEnumerable<OrganizationSponsorshipData> sponsorshipsData
    );
}
