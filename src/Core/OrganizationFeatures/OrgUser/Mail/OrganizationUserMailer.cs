using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Mail;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrgUser.Mail
{
    public class OrganizationUserMailer : HandlebarsMailService, IOrganizationUserMailer
    {
        private readonly IOrganizationUserRepository _organizationUserRepository;

        public OrganizationUserMailer(
            IOrganizationUserRepository organizationUserRepository,
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService,
            IMailEnqueuingService mailEnqueuingService) : base(globalSettings, mailDeliveryService, mailEnqueuingService)
        {
            _organizationUserRepository = organizationUserRepository;
        }

        public async Task SendInvitesAsync(IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> invites, Organization organization)
        {
            MailQueueMessage CreateMessage(string email, object model)
            {
                var message = CreateDefaultMessage($"Join {organization.Name}", email);
                return new MailQueueMessage(message, "OrganizationUserInvited", model);
            }

            var messageModels = invites.Select(invite => CreateMessage(invite.orgUser.Email,
                new OrganizationUserInvitedViewModel
                {
                    OrganizationName = CoreHelpers.SanitizeForEmail(organization.Name, false),
                    Email = WebUtility.UrlEncode(invite.orgUser.Email),
                    OrganizationId = invite.orgUser.OrganizationId.ToString(),
                    OrganizationUserId = invite.orgUser.Id.ToString(),
                    Token = WebUtility.UrlEncode(invite.token.Token),
                    ExpirationDate = $"{invite.token.ExpirationDate.ToLongDateString()} {invite.token.ExpirationDate.ToShortTimeString()} UTC",
                    OrganizationNameUrlEncoded = WebUtility.UrlEncode(organization.Name),
                    WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                    SiteName = _globalSettings.SiteName,
                    OrganizationCanSponsor = CheckOrganizationCanSponsor(organization),
                }
            ));

            await EnqueueMailAsync(messageModels);
        }

        public async Task SendOrganizationAcceptedEmailAsync(Organization organization, User acceptingUser)
        {
            var adminEmails = await GetOrganizationUserEmailsAsync(organization.Id, OrganizationUserType.Admin);

            var message = CreateDefaultMessage($"Action Required: {acceptingUser.Email} Needs to Be Confirmed", adminEmails);
            var model = new OrganizationUserAcceptedViewModel
            {
                OrganizationId = organization.Id,
                OrganizationName = CoreHelpers.SanitizeForEmail(organization.Name, false),
                UserIdentifier = acceptingUser.Email,
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "OrganizationUserAccepted", model);
            message.Category = "OrganizationUserAccepted";
            await SendEmailAsync(message);
        }

        public async Task SendOrganizationConfirmedEmail(Organization organization, User user)
        {
            var message = CreateDefaultMessage($"You Have Been Confirmed To {organization.Name}", user.Email);
            var model = new OrganizationUserConfirmedViewModel
            {
                OrganizationName = CoreHelpers.SanitizeForEmail(organization.Name, false),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "OrganizationUserConfirmed", model);
            message.Category = "OrganizationUserConfirmed";
            await SendEmailAsync(message);
        }

        private bool CheckOrganizationCanSponsor(Organization organization)
        {
            return StaticStore.GetPlan(organization.PlanType).Product == ProductType.Enterprise
                && !_globalSettings.SelfHosted;
        }

        private async Task<IEnumerable<string>> GetOrganizationUserEmailsAsync(Guid organizationId, OrganizationUserType orgUserType) =>
            (await _organizationUserRepository.GetManyByMinimumRoleAsync(organizationId, orgUserType))
            .Select(a => a.Email).Distinct();
    }
}
