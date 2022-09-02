using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class CloudSyncSponsorshipsCommand : ICloudSyncSponsorshipsCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IEventService _eventService;

    public CloudSyncSponsorshipsCommand(
    IOrganizationSponsorshipRepository organizationSponsorshipRepository,
    IEventService eventService)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _eventService = eventService;
    }

    public async Task<(OrganizationSponsorshipSyncData, IEnumerable<OrganizationSponsorship>)> SyncOrganization(Organization sponsoringOrg, IEnumerable<OrganizationSponsorshipData> sponsorshipsData)
    {
        if (sponsoringOrg == null)
        {
            throw new BadRequestException("Failed to sync sponsorship - missing organization.");
        }

        var (processedSponsorshipsData, sponsorshipsToEmailOffer) = sponsorshipsData.Any() ?
            await DoSyncAsync(sponsoringOrg, sponsorshipsData) :
            (sponsorshipsData, Array.Empty<OrganizationSponsorship>());

        await RecordEvent(sponsoringOrg);

        return (new OrganizationSponsorshipSyncData
        {
            SponsorshipsBatch = processedSponsorshipsData
        }, sponsorshipsToEmailOffer);
    }

    private async Task<(IEnumerable<OrganizationSponsorshipData> data, IEnumerable<OrganizationSponsorship> toOffer)> DoSyncAsync(Organization sponsoringOrg, IEnumerable<OrganizationSponsorshipData> sponsorshipsData)
    {
        var existingSponsorshipsDict = (await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id))
            .ToDictionary(i => i.SponsoringOrganizationUserId);

        var sponsorshipsToUpsert = new List<OrganizationSponsorship>();
        var sponsorshipIdsToDelete = new List<Guid>();
        var sponsorshipsToReturn = new List<OrganizationSponsorshipData>();

        foreach (var selfHostedSponsorship in sponsorshipsData)
        {
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(selfHostedSponsorship.PlanSponsorshipType)?.SponsoringProductType;
            if (requiredSponsoringProductType == null
                || StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                continue; // prevent unsupported sponsorships
            }

            if (!existingSponsorshipsDict.TryGetValue(selfHostedSponsorship.SponsoringOrganizationUserId, out var cloudSponsorship))
            {
                if (selfHostedSponsorship.ToDelete && selfHostedSponsorship.LastSyncDate == null)
                {
                    continue; // prevent invalid sponsorships in cloud. These should have been deleted by self hosted
                }
                if (OrgDisabledForMoreThanGracePeriod(sponsoringOrg))
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
            sponsorshipsToReturn.Add(selfHostedSponsorship);
        }
        var sponsorshipsToEmailOffer = sponsorshipsToUpsert.Where(s => s.Id == default).ToArray();
        if (sponsorshipsToUpsert.Any())
        {
            await _organizationSponsorshipRepository.UpsertManyAsync(sponsorshipsToUpsert);
        }
        if (sponsorshipIdsToDelete.Any())
        {
            await _organizationSponsorshipRepository.DeleteManyAsync(sponsorshipIdsToDelete);
        }

        return (sponsorshipsToReturn, sponsorshipsToEmailOffer);
    }

    /// <summary>
    /// True if Organization is disabled and the expiration date is more than three months ago
    /// </summary>
    /// <param name="organization"></param>
    private bool OrgDisabledForMoreThanGracePeriod(Organization organization) =>
        !organization.Enabled &&
        (
            !organization.ExpirationDate.HasValue ||
            DateTime.UtcNow.Subtract(organization.ExpirationDate.Value).TotalDays > 93
        );

    private async Task RecordEvent(Organization organization)
    {
        await _eventService.LogOrganizationEventAsync(organization, EventType.Organization_SponsorshipsSynced);
    }
}
