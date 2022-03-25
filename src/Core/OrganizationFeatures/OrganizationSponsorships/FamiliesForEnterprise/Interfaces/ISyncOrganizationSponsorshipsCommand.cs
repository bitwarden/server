using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface ISelfHostedSyncSponsorshipsCommand
    {
        Task SyncOrganization(Guid organizationId);
    }

    public interface ICloudSyncSponsorshipsCommand
    {
        Task<OrganizationSponsorshipSyncData> SyncOrganization(OrganizationSponsorshipSyncData syncData);
    }
}
