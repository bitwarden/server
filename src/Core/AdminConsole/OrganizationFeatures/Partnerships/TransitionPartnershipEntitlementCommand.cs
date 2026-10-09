using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class TransitionPartnershipEntitlementCommand(
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    IOrganizationPartnershipEntitlementRepository organizationPartnershipEntitlementRepository,
    IEventService eventService,
    TimeProvider timeProvider,
    GlobalSettings globalSettings)
    : ITransitionPartnershipEntitlementCommand
{
    private static readonly HashSet<PartnershipEntitlementReason> _partnerReasons =
    [
        PartnershipEntitlementReason.BillingLapse,
        PartnershipEntitlementReason.CustomerRequest,
        PartnershipEntitlementReason.SubscriptionEnded,
        PartnershipEntitlementReason.FraudHold,
        PartnershipEntitlementReason.PlanChange,
        PartnershipEntitlementReason.Administrative,
    ];

    public async Task<CommandResult<PartnershipEntitlementTransitionResult>> TransitionAsync(
        TransitionPartnershipEntitlementRequest request)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var effectiveAt = request.EffectiveAt ?? now;

        var requestError = ValidateRequest(request, effectiveAt, now);
        if (requestError is not null)
        {
            return requestError;
        }

        var partnership = await organizationPartnershipRepository.GetByIdAsync(request.OrganizationPartnershipId);
        if (partnership is null)
        {
            return new PartnershipNotFound();
        }

        // A customer can always leave, even when the partner's partnership is no longer active.
        if (partnership.Status != PartnershipStatus.Active && request.Action != PartnershipEntitlementAction.UserExit)
        {
            return new PartnershipNotActive();
        }

        var entitlement = await organizationPartnershipEntitlementRepository
            .GetByExternalIdAsync(partnership.Id, request.ExternalId);
        if (entitlement is null)
        {
            if (request.Action == PartnershipEntitlementAction.Activate)
            {
                return await BindingFailedAsync(partnership, new EntitlementNotProvisioned(), now);
            }
            return new EntitlementNotFound();
        }

        var expectedRevisionDate = entitlement.RevisionDate;

        if (request.Action == PartnershipEntitlementAction.UserExit && entitlement.UserId != request.UserId)
        {
            return new EntitlementNotFound();
        }

        if (PartnershipEntitlementRules.IsStale(entitlement, effectiveAt))
        {
            return new PartnershipEntitlementTransitionResult(
                entitlement, Applied: false, PartnershipEntitlementAppliedReasons.StaleTransition,
                entitlement.LastAppliedEffectiveDate);
        }

        var transitionError = request.Action switch
        {
            PartnershipEntitlementAction.Activate => Activate(entitlement, request.UserId!.Value, effectiveAt, now),
            PartnershipEntitlementAction.Suspend => Suspend(entitlement, effectiveAt),
            PartnershipEntitlementAction.Resume => Resume(entitlement, now),
            PartnershipEntitlementAction.Cancel => Cancel(entitlement, effectiveAt,
                PartnershipEntitlementState.Active, PartnershipEntitlementState.Suspended),
            PartnershipEntitlementAction.UserExit => UserExit(entitlement, effectiveAt),
            _ => new IllegalEntitlementTransition(),
        };

        if (transitionError is not null)
        {
            return request.Action == PartnershipEntitlementAction.Activate && transitionError is IPartnershipBindingFailure
                ? await BindingFailedAsync(partnership, transitionError, now)
                : transitionError;
        }

        entitlement.LastAppliedEffectiveDate = effectiveAt;
        entitlement.RevisionDate = now;
        if (!await organizationPartnershipEntitlementRepository.ReplaceIfUnchangedAsync(entitlement, expectedRevisionDate))
        {
            return new EntitlementConcurrentlyModified();
        }

        await eventService.LogOrganizationPartnershipEventAsync(
            partnership.OrganizationId, EventTypeFor(entitlement.State), effectiveAt);

        return new PartnershipEntitlementTransitionResult(entitlement, Applied: true, null, effectiveAt);
    }

    private static Error? ValidateRequest(TransitionPartnershipEntitlementRequest request, DateTime effectiveAt, DateTime now)
    {
        if (!PartnershipEntitlementRules.IsValidExternalId(request.ExternalId))
        {
            return new InvalidExternalId();
        }

        var requiresUser = request.Action is PartnershipEntitlementAction.Activate or PartnershipEntitlementAction.UserExit;
        if (requiresUser && request.UserId is null)
        {
            return new UserIdRequired();
        }

        if (request.Reason is { } reason && !IsAllowedReason(request.Action, reason))
        {
            return new InvalidEntitlementReason();
        }

        if (effectiveAt > now)
        {
            return new EffectiveAtInFuture();
        }

        return null;
    }

    private static bool IsAllowedReason(PartnershipEntitlementAction action, PartnershipEntitlementReason reason) =>
        action switch
        {
            PartnershipEntitlementAction.Activate => false,
            PartnershipEntitlementAction.UserExit => reason == PartnershipEntitlementReason.UserExit,
            _ => _partnerReasons.Contains(reason),
        };

    private static Error? Activate(OrganizationPartnershipEntitlement entitlement, Guid userId, DateTime effectiveAt, DateTime now)
    {
        switch (entitlement.State)
        {
            case PartnershipEntitlementState.Provisioned:
                entitlement.State = PartnershipEntitlementState.Active;
                entitlement.UserId = userId;
                entitlement.SetNewAccountRef();
                entitlement.BoundDate = effectiveAt;
                return null;
            case PartnershipEntitlementState.Canceled:
                if (!IsWithinResumeWindow(entitlement, now))
                {
                    return new EntitlementNotResumable();
                }
                if (entitlement.UserId != userId)
                {
                    return new EntitlementAlreadyBound();
                }
                Rebind(entitlement);
                return null;
            default:
                return new EntitlementAlreadyBound();
        }
    }

    private static Error? Suspend(OrganizationPartnershipEntitlement entitlement, DateTime effectiveAt)
    {
        if (entitlement.State != PartnershipEntitlementState.Active)
        {
            return new IllegalEntitlementTransition();
        }

        entitlement.State = PartnershipEntitlementState.Suspended;
        entitlement.SuspendedDate = effectiveAt;
        return null;
    }

    private static Error? Resume(OrganizationPartnershipEntitlement entitlement, DateTime now)
    {
        switch (entitlement.State)
        {
            case PartnershipEntitlementState.Suspended:
                Rebind(entitlement);
                return null;
            case PartnershipEntitlementState.Canceled:
                if (!IsWithinResumeWindow(entitlement, now))
                {
                    return new EntitlementNotResumable();
                }
                Rebind(entitlement);
                return null;
            default:
                return new IllegalEntitlementTransition();
        }
    }

    private Error? Cancel(OrganizationPartnershipEntitlement entitlement, DateTime effectiveAt,
        params PartnershipEntitlementState[] allowedFrom)
    {
        if (!allowedFrom.Contains(entitlement.State))
        {
            return new IllegalEntitlementTransition();
        }

        entitlement.State = PartnershipEntitlementState.Canceled;
        entitlement.CanceledDate = effectiveAt;
        entitlement.ResumeWindowExpirationDate = effectiveAt + globalSettings.Partnerships.ResumeWindow;
        return null;
    }

    /// <summary>
    /// Ends the sponsorship and releases the binding at once, with no resume window, so only the customer's own
    /// activation after a re-provision can attach the entitlement to their account again.
    /// </summary>
    private static Error? UserExit(OrganizationPartnershipEntitlement entitlement, DateTime effectiveAt)
    {
        if (entitlement.State != PartnershipEntitlementState.Active)
        {
            return new IllegalEntitlementTransition();
        }

        entitlement.State = PartnershipEntitlementState.Canceled;
        entitlement.CanceledDate = effectiveAt;
        entitlement.ResumeWindowExpirationDate = null;
        entitlement.UserId = null;
        entitlement.AccountRef = null;
        return null;
    }

    /// <summary>
    /// Returns a suspended or held entitlement to Active on its existing account binding.
    /// </summary>
    private static void Rebind(OrganizationPartnershipEntitlement entitlement)
    {
        entitlement.State = PartnershipEntitlementState.Active;
        entitlement.SuspendedDate = null;
        entitlement.CanceledDate = null;
        entitlement.ResumeWindowExpirationDate = null;
    }

    /// <summary>
    /// Measured against now rather than effectiveAt because the hold can be released once the window ends.
    /// </summary>
    private static bool IsWithinResumeWindow(OrganizationPartnershipEntitlement entitlement, DateTime now) =>
        entitlement.UserId is not null &&
        entitlement.ResumeWindowExpirationDate is { } expiration &&
        now < expiration;

    private async Task<Error> BindingFailedAsync(OrganizationPartnership partnership, Error error, DateTime now)
    {
        await eventService.LogOrganizationPartnershipEventAsync(
            partnership.OrganizationId, EventType.PartnershipEntitlement_BindingFailed, now);
        return error;
    }

    private static EventType EventTypeFor(PartnershipEntitlementState state) =>
        state switch
        {
            PartnershipEntitlementState.Active => EventType.PartnershipEntitlement_Activated,
            PartnershipEntitlementState.Suspended => EventType.PartnershipEntitlement_Suspended,
            PartnershipEntitlementState.Canceled => EventType.PartnershipEntitlement_Deactivated,
            _ => throw new InvalidOperationException($"No lifecycle event for state {state}."),
        };
}
