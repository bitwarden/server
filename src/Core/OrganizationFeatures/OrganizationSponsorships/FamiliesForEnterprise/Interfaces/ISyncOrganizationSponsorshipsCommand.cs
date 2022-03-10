using System;
using System.Threading.Tasks;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface ISyncOrganizationSponsorshipsCommand
    {
        Task SyncOrganization(Guid organizationId);
    }

    public interface ICloudSyncOrganizationSponsorshipsCommand
    {
        Task SyncOrganization(OrganizationSponsorshipSyncRequestModel model);
    }
}
