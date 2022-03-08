using System;
using System.Threading.Tasks;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface ISyncOrganizationSponsorshipsCommand
    {
        Task SyncOrganization(Guid organizationId);
    }
}
