﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class ValidateSponsorshipCommand : CancelSponsorshipCommand, IValidateSponsorshipCommand
{
    private readonly IPaymentService _paymentService;
    private readonly IMailService _mailService;
    private readonly ILogger<ValidateSponsorshipCommand> _logger;

    public ValidateSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository,
        IPaymentService paymentService,
        IMailService mailService,
        ILogger<ValidateSponsorshipCommand> logger) : base(organizationSponsorshipRepository, organizationRepository)
    {
        _paymentService = paymentService;
        _mailService = mailService;
        _logger = logger;
    }

    public async Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId)
    {
        var sponsoredOrganization = await _organizationRepository.GetByIdAsync(sponsoredOrganizationId);

        if (sponsoredOrganization == null)
        {
            _logger.LogWarning("Sponsored Organization {OrganizationId} does not exist", sponsoredOrganizationId);
            return false;
        }

        var existingSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

        if (existingSponsorship == null)
        {
            _logger.LogWarning("Existing sponsorship for sponsored Organization {SponsoredOrganizationId} does not exist", sponsoredOrganizationId);

            await CancelSponsorshipAsync(sponsoredOrganization, null);
            return false;
        }

        if (existingSponsorship.SponsoringOrganizationId == null)
        {
            _logger.LogWarning("Sponsoring OrganizationId is null for sponsored Organization {SponsoredOrganizationId}", sponsoredOrganizationId);

            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        if (existingSponsorship.SponsoringOrganizationUserId == default)
        {
            _logger.LogWarning("Sponsoring OrganizationUserId is null for sponsored Organization {SponsoredOrganizationId}", sponsoredOrganizationId);

            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        if (existingSponsorship.PlanSponsorshipType == null)
        {
            _logger.LogWarning("PlanSponsorshipType is null for sponsored Organization {SponsoredOrganizationId}", sponsoredOrganizationId);

            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        if (existingSponsorship.SponsoringOrganizationId == null)
        {
            _logger.LogWarning("Sponsoring OrganizationId is null for sponsored Organization {SponsoredOrganizationId}", sponsoredOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        if (existingSponsorship.SponsoringOrganizationUserId == default)
        {
            _logger.LogWarning("Sponsoring OrganizationUserId is null for sponsored Organization {SponsoredOrganizationId}", sponsoredOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        if (existingSponsorship.PlanSponsorshipType == null)
        {
            _logger.LogWarning("PlanSponsorshipType is null for sponsored Organization {SponsoredOrganizationId}", sponsoredOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(existingSponsorship.PlanSponsorshipType.Value);

        var sponsoringOrganization = await _organizationRepository
            .GetByIdAsync(existingSponsorship.SponsoringOrganizationId.Value);

        if (sponsoringOrganization == null)
        {
            _logger.LogWarning("Sponsoring Organization {SponsoringOrganizationId} does not exist", existingSponsorship.SponsoringOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        if (OrgDisabledForMoreThanGracePeriod(sponsoringOrganization))
        {
            _logger.LogWarning("Sponsoring Organization {SponsoringOrganizationId} is disabled for more than 3 months.", sponsoringOrganization.Id);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);

            return false;
        }

        if (existingSponsorship.IsAdminInitiated && !sponsoringOrganization.UseAdminSponsoredFamilies)
        {
            _logger.LogWarning("Admin initiated sponsorship for sponsored Organization {SponsoredOrganizationId} is not allowed because sponsoring organization does not have UseAdminSponsoredFamilies enabled", sponsoredOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        var sponsoringOrgProductTier = sponsoringOrganization.PlanType.GetProductTier();

        if (sponsoredPlan.SponsoringProductTierType != sponsoringOrgProductTier)
        {
            _logger.LogWarning("Sponsoring Organization {SponsoringOrganizationId} is not on the required product type.", sponsoringOrganization.Id);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);

            return false;
        }

        if (existingSponsorship.ToDelete)
        {
            _logger.LogWarning("Sponsorship for sponsored Organization {SponsoredOrganizationId} is marked for deletion", sponsoredOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);

            return false;
        }

        if (SponsorshipIsSelfHostedOutOfSync(existingSponsorship))
        {
            _logger.LogWarning("Sponsorship for sponsored Organization {SponsoredOrganizationId} is out of sync with self-hosted instance.", sponsoredOrganizationId);
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);

            return false;
        }

        _logger.LogInformation("Sponsorship for sponsored Organization {SponsoredOrganizationId} is valid", sponsoredOrganizationId);
        return true;
    }

    private async Task CancelSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
    {
        await Task.CompletedTask; // this is intentional

        // if (sponsoredOrganization != null)
        // {
        //     await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);
        //     await _organizationRepository.UpsertAsync(sponsoredOrganization);
        //
        //     try
        //     {
        //         if (sponsorship != null)
        //         {
        //             await _mailService.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(
        //                 sponsoredOrganization.BillingEmailAddress(),
        //                 sponsorship.ValidUntil ?? DateTime.UtcNow.AddDays(15));
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError(e, "Error sending Family sponsorship removed email.");
        //     }
        // }
        // await base.DeleteSponsorshipAsync(sponsorship);
    }

    /// <summary>
    /// True if Sponsorship is from a self-hosted instance that has failed to sync for more than 6 months
    /// </summary>
    /// <param name="sponsorship"></param>
    private bool SponsorshipIsSelfHostedOutOfSync(OrganizationSponsorship sponsorship) =>
        sponsorship.LastSyncDate.HasValue &&
        DateTime.UtcNow.Subtract(sponsorship.LastSyncDate.Value).TotalDays > 182.5;

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
}
