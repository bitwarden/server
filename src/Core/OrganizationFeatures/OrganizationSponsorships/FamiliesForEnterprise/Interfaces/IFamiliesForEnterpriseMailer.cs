using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface IFamiliesForEnterpriseMailer
    {
        Task SendFamiliesForEnterpriseOfferEmailAsync(Organization sponsoringOrg, OrganizationSponsorship sponsorship, bool existingAccount, string token);
        Task BulkSendFamiliesForEnterpriseOfferEmailAsync(Organization SponsoringOrg, IEnumerable<(OrganizationSponsorship sponsorship, bool ExistingAccount, string Token)> invites);
        Task SendFamiliesForEnterpriseRedeemedEmailsAsync(string familyUserEmail, string sponsorEmail);
        Task SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(string email, DateTime expirationDate);
    }
}
