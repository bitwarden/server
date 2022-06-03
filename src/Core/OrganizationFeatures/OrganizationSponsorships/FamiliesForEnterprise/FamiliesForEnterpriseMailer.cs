using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

        public async Task SendFamiliesForEnterpriseOfferEmailAsync(string sponsorOrgName, string email, bool existingAccount, string token) =>
            await BulkSendFamiliesForEnterpriseOfferEmailAsync(sponsorOrgName, new[] { (email, existingAccount, token) });

        public async Task BulkSendFamiliesForEnterpriseOfferEmailAsync(string sponsorOrgName, IEnumerable<(string Email, bool ExistingAccount, string Token)> invites)
        {
            MailQueueMessage CreateMessage((string Email, bool ExistingAccount, string Token) invite)
            {
                var message = new MailMessage
                {
                    Subject = "Accept Your Free Families Subscription",
                    ToEmails = new[] { invite.Email },
                    Category = "FamiliesForEnterpriseOffer",
                };
                var model = new FamiliesForEnterpriseOfferViewModel
                {
                    SponsorOrgName = sponsorOrgName,
                    SponsoredEmail = WebUtility.UrlEncode(invite.Email),
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

        public async Task SendFamiliesForEnterpriseRedeemedEmailsAsync(string familyUserEmail, string sponsorEmail)
        {
            // Email family user
            await SendFamiliesForEnterpriseInviteRedeemedToFamilyUserEmailAsync(familyUserEmail);

            // Email enterprise org user
            await SendFamiliesForEnterpriseInviteRedeemedToEnterpriseUserEmailAsync(sponsorEmail);
        }

        private async Task SendFamiliesForEnterpriseInviteRedeemedToFamilyUserEmailAsync(string email)
        {
            var message = new MailMessage
            {
                Subject = "Success! Families Subscription Accepted",
                ToEmails = new[] { email },
                Category = "FamilyForEnterpriseRedeemedToFamilyUser"
            };
            await _mailService.EnqueueMailAsync(message, "FamiliesForEnterprise.FamiliesForEnterpriseRedeemedToFamilyUser", new BaseMailModel());
        }

        private async Task SendFamiliesForEnterpriseInviteRedeemedToEnterpriseUserEmailAsync(string email)
        {
            var message = new MailMessage
            {
                Subject = "Success! Families Subscription Accepted",
                ToEmails = new[] { email },
                Category = "FamilyForEnterpriseRedeemedToEnterpriseUser",
            };
            await _mailService.EnqueueMailAsync(message, "FamiliesForEnterprise.FamiliesForEnterpriseRedeemedToEnterpriseUser", new BaseMailModel());
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
