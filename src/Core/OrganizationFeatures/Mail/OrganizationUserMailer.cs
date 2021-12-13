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
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.Mail
{
    public class OrganizationUserMailer : HandlebarsMailService, IOrganizationUserMailer
    {
        readonly IDataProtector _dataProtector;
        readonly IOrganizationRepository _organizationRepository;
        readonly IOrganizationUserRepository _organizationUserRepository;

        public OrganizationUserMailer(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IDataProtectionProvider dataProtectionProvider,
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService,
            IMailEnqueuingService mailEnqueuingService) : base(globalSettings, mailDeliveryService, mailEnqueuingService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            // TODO: change protector string?
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationServiceDataProtector");
        }

        public async Task SendOrganizationAutoscaledEmailAsync(Organization organization, int initialSeatCount)
        {
            if (organization.OwnersNotifiedOfAutoscaling.HasValue)
            {
                return;
            }

            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                OrganizationUserType.Owner)).Select(u => u.Email).Distinct();

            var message = CreateDefaultMessage($"{organization.Name} Seat Count Has Increased", ownerEmails);
            var model = new OrganizationSeatsAutoscaledViewModel
            {
                OrganizationId = organization.Id,
                InitialSeatCount = initialSeatCount,
                CurrentSeatCount = organization.Seats.Value,
            };

            await AddMessageContentAsync(message, "OrganizationSeatsAutoscaled", model);
            message.Category = "OrganizationSeatsAutoscaled";
            await SendEmailAsync(message);

            organization.OwnersNotifiedOfAutoscaling = DateTime.UtcNow;
            await _organizationRepository.UpsertAsync(organization);
        }

        public async Task SendOrganizationMaxSeatLimitReachedEmailAsync(Organization organization, int maxSeatCount)
        {
            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                            OrganizationUserType.Owner)).Select(u => u.Email).Distinct();

            var message = CreateDefaultMessage($"{organization.Name} Seat Limit Reached", ownerEmails);
            var model = new OrganizationSeatsMaxReachedViewModel
            {
                OrganizationId = organization.Id,
                MaxSeatCount = maxSeatCount,
            };

            await AddMessageContentAsync(message, "OrganizationSeatsMaxReached", model);
            message.Category = "OrganizationSeatsMaxReached";
            await SendEmailAsync(message);
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
