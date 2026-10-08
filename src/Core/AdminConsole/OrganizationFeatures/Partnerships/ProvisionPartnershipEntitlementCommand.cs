using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class ProvisionPartnershipEntitlementCommand(
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    IOrganizationPartnershipEntitlementRepository organizationPartnershipEntitlementRepository,
    IEventService eventService,
    TimeProvider timeProvider)
    : IProvisionPartnershipEntitlementCommand
{
    public async Task<CommandResult<ProvisionPartnershipEntitlementResult>> ProvisionAsync(
        ProvisionPartnershipEntitlementRequest request)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var effectiveAt = request.EffectiveAt ?? now;

        if (!PartnershipEntitlementRules.IsValidExternalId(request.ExternalId))
        {
            return new InvalidExternalId();
        }

        if (request.Metadata?.Count > ProvisionPartnershipEntitlementRequest.MaxMetadataKeys)
        {
            return new TooManyMetadataKeys();
        }

        if (effectiveAt > now)
        {
            return new EffectiveAtInFuture();
        }

        var partnership = await organizationPartnershipRepository.GetByIdAsync(request.OrganizationPartnershipId);
        if (partnership is null)
        {
            return new PartnershipNotFound();
        }

        if (partnership.Status != PartnershipStatus.Active)
        {
            return new PartnershipNotActive();
        }

        var existing = await organizationPartnershipEntitlementRepository
            .GetByExternalIdAsync(partnership.Id, request.ExternalId);

        if (existing is null)
        {
            var entitlement = new OrganizationPartnershipEntitlement
            {
                OrganizationPartnershipId = partnership.Id,
                ExternalId = request.ExternalId,
                State = PartnershipEntitlementState.Provisioned,
                LastAppliedEffectiveDate = effectiveAt,
                CreationDate = now,
                RevisionDate = now,
            };
            entitlement.SetMetadata(request.Metadata);
            entitlement.SetNewId();

            await organizationPartnershipEntitlementRepository.CreateAsync(entitlement);
            await LogProvisionedAsync(partnership, effectiveAt);

            return new ProvisionPartnershipEntitlementResult(entitlement, Created: true, Applied: true, null, effectiveAt);
        }

        if (existing.State != PartnershipEntitlementState.Canceled)
        {
            return new ProvisionPartnershipEntitlementResult(
                existing, Created: false, Applied: false, null, existing.LastAppliedEffectiveDate);
        }

        if (PartnershipEntitlementRules.IsStale(existing, effectiveAt))
        {
            return new ProvisionPartnershipEntitlementResult(
                existing, Created: false, Applied: false,
                PartnershipEntitlementAppliedReasons.StaleTransition, existing.LastAppliedEffectiveDate);
        }

        existing.State = PartnershipEntitlementState.Provisioned;
        existing.UserId = null;
        existing.AccountRef = null;
        existing.BoundDate = null;
        existing.SuspendedDate = null;
        existing.CanceledDate = null;
        existing.ResumeWindowExpirationDate = null;
        existing.SetMetadata(request.Metadata);
        existing.LastAppliedEffectiveDate = effectiveAt;
        existing.RevisionDate = now;

        await organizationPartnershipEntitlementRepository.ReplaceAsync(existing);
        await LogProvisionedAsync(partnership, effectiveAt);

        return new ProvisionPartnershipEntitlementResult(existing, Created: false, Applied: true, null, effectiveAt);
    }

    private Task LogProvisionedAsync(OrganizationPartnership partnership, DateTime effectiveAt) =>
        eventService.LogOrganizationPartnershipEventAsync(
            partnership.OrganizationId, EventType.PartnershipEntitlement_Provisioned, effectiveAt);
}
