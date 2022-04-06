﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSyncSponsorshipsCommand : ICloudSyncSponsorshipsCommand
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;
        private readonly IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> _tokenFactory;


        public CloudSyncSponsorshipsCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        IMailService mailService,
        IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> tokenFactory)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _userRepository = userRepository;
            _mailService = mailService;
            _tokenFactory = tokenFactory;
        }

        public async Task<OrganizationSponsorshipSyncData> SyncOrganization(Organization sponsoringOrg, IEnumerable<OrganizationSponsorshipData> sponsorshipsData)
        {
            if (sponsoringOrg == null)
            {
                throw new BadRequestException("Failed to sync sponsorship - missing organization.");
            }
            if (!sponsorshipsData.Any())
            {
                return new OrganizationSponsorshipSyncData
                {
                    SponsorshipsBatch = sponsorshipsData
                };
            }

            var existingSponsorshipsDict = (await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id))
                .ToDictionary(i => i.SponsoringOrganizationUserId);

            var sponsorshipsToUpsert = new List<OrganizationSponsorship>();
            var sponsorshipIdsToDelete = new List<Guid>();

            foreach (var selfHostedSponsorship in sponsorshipsData)
            {
                var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(selfHostedSponsorship.PlanSponsorshipType)?.SponsoringProductType;
                if (requiredSponsoringProductType == null ||
                    StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
                {
                    throw new BadRequestException("Specified Organization does not support this type of sponsorship.");
                }

                var cloudSponsorship = existingSponsorshipsDict[selfHostedSponsorship.SponsoringOrganizationUserId];
                if (cloudSponsorship == null)
                {
                    if (selfHostedSponsorship.ToDelete && selfHostedSponsorship.LastSyncDate == null)
                    {
                        continue; // prevent invalid sponsorships in cloud. These should have been deleted by self hosted
                    }
                    if (!sponsoringOrg.Enabled)
                    {
                        continue; // prevent new sponsorships from disabled orgs
                    }
                    cloudSponsorship = new OrganizationSponsorship
                    {
                        SponsoringOrganizationId = sponsoringOrg.Id,
                        SponsoringOrganizationUserId = selfHostedSponsorship.SponsoringOrganizationUserId,
                        FriendlyName = selfHostedSponsorship.FriendlyName,
                        OfferedToEmail = selfHostedSponsorship.OfferedToEmail,
                        PlanSponsorshipType = selfHostedSponsorship.PlanSponsorshipType,
                        LastSyncDate = DateTime.UtcNow,
                    };
                }
                else
                {
                    cloudSponsorship.LastSyncDate = DateTime.UtcNow;
                }

                if (selfHostedSponsorship.ToDelete)
                {
                    if (cloudSponsorship.SponsoredOrganizationId == null)
                    {
                        sponsorshipIdsToDelete.Add(cloudSponsorship.Id);
                        selfHostedSponsorship.CloudSponsorshipRemoved = true;
                    }
                    else
                    {
                        cloudSponsorship.ToDelete = true;
                    }
                }
                sponsorshipsToUpsert.Add(cloudSponsorship);

                selfHostedSponsorship.ValidUntil = cloudSponsorship.ValidUntil;
                selfHostedSponsorship.LastSyncDate = DateTime.UtcNow;
            }
            var sponsorshipsToEmailOffer = sponsorshipsToUpsert.Where(s => s.Id == null);
            await _organizationSponsorshipRepository.UpsertManyAsync(sponsorshipsToUpsert);
            await BulkSendSponsorshipOfferAsync(sponsoringOrg.Name, sponsorshipsToEmailOffer);
            await _organizationSponsorshipRepository.DeleteManyAsync(sponsorshipIdsToDelete);

            return new OrganizationSponsorshipSyncData
            {
                SponsorshipsBatch = sponsorshipsData
            };
        }
        private async Task BulkSendSponsorshipOfferAsync(string sponsoringOrgName, IEnumerable<OrganizationSponsorship> sponsorships)
        {
            var invites = new List<(string, bool, string)>();
            foreach (var sponsorship in sponsorships)
            {
                var user = await _userRepository.GetByEmailAsync(sponsorship.OfferedToEmail);
                var isExistingAccount = user != null;
                invites.Add((sponsorship.OfferedToEmail, user != null, _tokenFactory.Protect(new OrganizationSponsorshipOfferTokenable(sponsorship))));
            }

            await _mailService.BulkSendFamiliesForEnterpriseOfferEmailAsync(sponsoringOrgName, invites);
        }

    }
}
