using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class ExpirePartnershipResumeWindowsCommand(
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    IOrganizationPartnershipEntitlementRepository organizationPartnershipEntitlementRepository,
    IEventService eventService,
    TimeProvider timeProvider,
    ILogger<ExpirePartnershipResumeWindowsCommand> logger)
    : IExpirePartnershipResumeWindowsCommand
{
    public async Task<CommandResult<int>> ExpireAsync()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entitlements = await organizationPartnershipEntitlementRepository
            .GetManyCanceledWithExpiredResumeWindowAsync(now);

        var partnerships = new Dictionary<Guid, OrganizationPartnership?>();
        var released = 0;

        foreach (var entitlement in entitlements)
        {
            try
            {
                if (!partnerships.TryGetValue(entitlement.OrganizationPartnershipId, out var partnership))
                {
                    partnership = await organizationPartnershipRepository.GetByIdAsync(entitlement.OrganizationPartnershipId);
                    partnerships[entitlement.OrganizationPartnershipId] = partnership;
                }

                if (partnership is null)
                {
                    logger.LogWarning(
                        "Skipped resume window expiry for partnership entitlement {EntitlementId}: partnership not found",
                        entitlement.Id);
                    continue;
                }

                entitlement.UserId = null;
                entitlement.AccountRef = null;
                entitlement.RevisionDate = now;
                await organizationPartnershipEntitlementRepository.ReplaceAsync(entitlement);
                released++;

                await eventService.LogOrganizationPartnershipEventAsync(
                    partnership.OrganizationId,
                    EventType.PartnershipEntitlement_ResumeWindowExpired,
                    entitlement.ResumeWindowExpirationDate);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to expire resume window for partnership entitlement {EntitlementId}", entitlement.Id);
            }
        }

        return released;
    }
}
