using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class CancelSponsorshipCommand
    {
        protected readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        protected readonly IOrganizationRepository _organizationRepository;

        public CancelSponsorshipCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
        }

        protected virtual async Task CancelSponsorshipAsync(OrganizationSponsorship sponsorship = null)
        {
            if (sponsorship == null)
            {
                return;
            }

            // Initialize the record as available
            sponsorship.SponsoredOrganizationId = null;
            sponsorship.FriendlyName = null;
            sponsorship.OfferedToEmail = null;
            sponsorship.PlanSponsorshipType = null;
            sponsorship.ToDelete = true;

            if (sponsorship.ValidUntil.HasValue && sponsorship.ValidUntil.Value < DateTime.UtcNow)
            {
                await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
            }
            else
            {
                await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
            }
        }
    }
}
