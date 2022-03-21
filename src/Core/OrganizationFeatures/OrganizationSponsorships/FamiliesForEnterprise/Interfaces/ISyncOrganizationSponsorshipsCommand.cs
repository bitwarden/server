using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface ISelfHostedSyncOrganizationSponsorshipsCommand
    {
        Task SyncOrganization(Guid organizationId);
    }

    public interface ICloudSyncOrganizationSponsorshipsCommand
    {
        Task<OrganizationSponsorshipSyncModel> SyncOrganization(Organization sponsoringOrg, IEnumerable<OrganizationSponsorshipModel> sponsorshipsBatch);
    }
}
