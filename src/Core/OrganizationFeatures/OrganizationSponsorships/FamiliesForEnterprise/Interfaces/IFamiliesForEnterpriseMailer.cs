using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface IFamiliesForEnterpriseMailer
    {
        Task SendFamiliesForEnterpriseOfferEmailAsync(string sponsorOrgName, string email, bool existingAccount, string token);
        Task BulkSendFamiliesForEnterpriseOfferEmailAsync(string SponsorOrgName, IEnumerable<(string Email, bool ExistingAccount, string Token)> invites);
        Task SendFamiliesForEnterpriseRedeemedEmailsAsync(string familyUserEmail, string sponsorEmail);
        Task SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(string email, DateTime expirationDate);
    }
}
