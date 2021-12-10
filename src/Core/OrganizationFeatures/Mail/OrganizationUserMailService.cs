using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Mail;
using Bit.Core.Models.Table;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.Mail
{
    public class OrganizationUserMailService : HandlebarsMailService, IOrganizationUserMailService
    {
        readonly IDataProtector _dataProtector;
        public OrganizationUserMailService(
            IDataProtectionProvider dataProtectionProvider,
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService,
            IMailEnqueuingService mailEnqueuingService) : base(globalSettings, mailDeliveryService, mailEnqueuingService)
        {
            // TODO: change protector string?
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationServiceDataProtector");
        }

        public async Task SendInvitesAsync(IEnumerable<OrganizationUser> orgUsers, Organization organization)
        {
            string MakeToken(OrganizationUser orgUser) =>
                _dataProtector.Protect($"OrganizationUserInvite {orgUser.Id} {orgUser.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

            await BulkSendOrganizationInviteEmailAsync(organization.Name, CheckOrganizationCanSponsor(organization),
                orgUsers.Select(o => (o, new ExpiringToken(MakeToken(o), DateTime.UtcNow.AddDays(5)))));
        }

        private async Task BulkSendOrganizationInviteEmailAsync(string organizationName, bool organizationCanSponsor, IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> invites)
        {
            MailQueueMessage CreateMessage(string email, object model)
            {
                var message = CreateDefaultMessage($"Join {organizationName}", email);
                return new MailQueueMessage(message, "OrganizationUserInvited", model);
            }

            var messageModels = invites.Select(invite => CreateMessage(invite.orgUser.Email,
                new OrganizationUserInvitedViewModel
                {
                    OrganizationName = CoreHelpers.SanitizeForEmail(organizationName, false),
                    Email = WebUtility.UrlEncode(invite.orgUser.Email),
                    OrganizationId = invite.orgUser.OrganizationId.ToString(),
                    OrganizationUserId = invite.orgUser.Id.ToString(),
                    Token = WebUtility.UrlEncode(invite.token.Token),
                    ExpirationDate = $"{invite.token.ExpirationDate.ToLongDateString()} {invite.token.ExpirationDate.ToShortTimeString()} UTC",
                    OrganizationNameUrlEncoded = WebUtility.UrlEncode(organizationName),
                    WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                    SiteName = _globalSettings.SiteName,
                    OrganizationCanSponsor = organizationCanSponsor,
                }
            ));

            await EnqueueMailAsync(messageModels);
        }

        private bool CheckOrganizationCanSponsor(Organization organization)
        {
            return StaticStore.GetPlan(organization.PlanType).Product == ProductType.Enterprise
                && !_globalSettings.SelfHosted;
        }
    }
}
