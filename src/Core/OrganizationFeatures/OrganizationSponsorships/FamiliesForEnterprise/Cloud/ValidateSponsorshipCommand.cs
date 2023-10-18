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
            return false;
        }

        var existingSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

        if (existingSponsorship == null)
        {
            await CancelSponsorshipAsync(sponsoredOrganization, null);
            return false;
        }

        if (existingSponsorship.SponsoringOrganizationId == null || existingSponsorship.SponsoringOrganizationUserId == default || existingSponsorship.PlanSponsorshipType == null)
        {
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }
        var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(existingSponsorship.PlanSponsorshipType.Value);

        var sponsoringOrganization = await _organizationRepository
            .GetByIdAsync(existingSponsorship.SponsoringOrganizationId.Value);
        if (sponsoringOrganization == null)
        {
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        var sponsoringOrgPlan = Utilities.StaticStore.GetPlan(sponsoringOrganization.PlanType);
        if (OrgDisabledForMoreThanGracePeriod(sponsoringOrganization) ||
            sponsoredPlan.SponsoringProductType != sponsoringOrgPlan.Product ||
            existingSponsorship.ToDelete ||
            SponsorshipIsSelfHostedOutOfSync(existingSponsorship))
        {
            await CancelSponsorshipAsync(sponsoredOrganization, existingSponsorship);
            return false;
        }

        return true;
    }

    protected async Task CancelSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
    {
        if (sponsoredOrganization != null)
        {
            await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);

            try
            {
                if (sponsorship != null)
                {
                    await _mailService.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(
                        sponsoredOrganization.BillingEmailAddress(),
                        sponsorship.ValidUntil ?? DateTime.UtcNow.AddDays(15));
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error sending Family sponsorship removed email.", e);
            }
        }
        await base.DeleteSponsorshipAsync(sponsorship);
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
