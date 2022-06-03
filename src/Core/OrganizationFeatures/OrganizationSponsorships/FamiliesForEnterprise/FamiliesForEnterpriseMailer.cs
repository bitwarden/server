using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Bit.Core.Models.Mail.FamiliesForEnterprise;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class FamiliesForEnterpriseMailer : IFamiliesForEnterpriseMailer
    {
        private readonly IMailService _mailService;
        private readonly IGlobalSettings _globalSettings;

        public FamiliesForEnterpriseMailer(IMailService mailService, GlobalSettings globalSettings)
        {
            _mailService = mailService;
            _globalSettings = globalSettings;
        }

        public async Task SendFamiliesForEnterpriseOfferEmailAsync(Organization sponsoringOrg, OrganizationSponsorship sponsorship, bool existingAccount, string token) =>
            await BulkSendFamiliesForEnterpriseOfferEmailAsync(sponsoringOrg, new[] { (sponsorship, existingAccount, token) });

        public async Task BulkSendFamiliesForEnterpriseOfferEmailAsync(Organization sponsoringOrg, IEnumerable<(OrganizationSponsorship sponsorship, bool ExistingAccount, string Token)> invites)
        {
            MailQueueMessage CreateMessage((OrganizationSponsorship Sponsorship, bool ExistingAccount, string Token) invite)
            {
                var message = new MailMessage
                {
                    Subject = "Accept Your Free Families Subscription",
                    ToEmails = new[] { invite.Sponsorship.OfferedToEmail },
                    Category = "FamiliesForEnterpriseOffer",
                };
                var model = new FamiliesForEnterpriseOfferViewModel
                {
                    SponsoringOrgName = sponsoringOrg.Name,
                    SponsoredEmail = WebUtility.UrlEncode(invite.Sponsorship.OfferedToEmail),
                    ExistingAccount = invite.ExistingAccount,
                    WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                    SiteName = _globalSettings.SiteName,
                    SponsorshipToken = invite.Token,
                };
                var templateName = invite.ExistingAccount ?
                    "FamiliesForEnterprise.FamiliesForEnterpriseOfferExistingAccount" :
                    "FamiliesForEnterprise.FamiliesForEnterpriseOfferNewAccount";

                return new MailQueueMessage(message, templateName, model);
            }
            var messageModels = invites.Select(invite => CreateMessage(invite));
            await _mailService.EnqueueMailAsync(messageModels);
        }

        public async Task SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(string email, DateTime expirationDate)
        {
            var message = new MailMessage
            {
                Subject = "Your Families Sponsorship was Removed",
                ToEmails = new[] { email },
                Category = "FamiliesForEnterpriseSponsorshipReverting"
            };
            var model = new FamiliesForEnterpriseSponsorshipRevertingViewModel
            {
                ExpirationDate = expirationDate,
            };
            await _mailService.EnqueueMailAsync(message, "FamiliesForEnterprise.FamiliesForEnterpriseSponsorshipReverting", model);
        }
    }
}
