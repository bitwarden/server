using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IOrganizationSponsorshipService
    {
        Task CreateSponsorshipAsync(Guid sponsoringOrgId, OrganizationSponsorshipRequestModel model);
        Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId);
        Task RedeemSponsorshipAsync(string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model);
        Task ResendSponsorshipOfferAsync(Guid sponsoringOrgId);
        Task RevokeSponsorshipAsync(Guid sponsoringOrganizationId);
        Task RemoveSponsorshipAsync(Guid sponsoredOrgId);
    }
}
