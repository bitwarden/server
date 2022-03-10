
using System.Threading.Tasks;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSyncOrganizationSponsorshipsCommand : ICloudSyncOrganizationSponsorshipsCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;


        public CloudSyncOrganizationSponsorshipsCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ILogger<CloudSyncOrganizationSponsorshipsCommand> logger)
        {
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
        }

        public Task SyncOrganization(OrganizationSponsorshipSyncRequestModel model) => throw new System.NotImplementedException();

    }
}
