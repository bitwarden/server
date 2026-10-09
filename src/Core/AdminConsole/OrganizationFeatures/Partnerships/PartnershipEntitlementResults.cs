using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public static class PartnershipEntitlementAppliedReasons
{
    /// <summary>
    /// The request's effectiveAt is older than the entitlement's last applied transition.
    /// </summary>
    public const string StaleTransition = "stale_transition";
}

/// <param name="Entitlement">The entitlement after the request; unchanged when <paramref name="Applied"/> is false.</param>
/// <param name="Applied">Whether the transition was applied.</param>
/// <param name="AppliedReason">Why the transition was not applied, e.g. <see cref="PartnershipEntitlementAppliedReasons.StaleTransition"/>.</param>
/// <param name="LastAppliedAt">The entitlement's <see cref="OrganizationPartnershipEntitlement.LastAppliedEffectiveDate"/>.</param>
public record PartnershipEntitlementTransitionResult(
    OrganizationPartnershipEntitlement Entitlement,
    bool Applied,
    string? AppliedReason,
    DateTime LastAppliedAt);

/// <param name="Entitlement">The provisioned entitlement, or the existing one when nothing was applied.</param>
/// <param name="Created">Whether a new entitlement record was inserted. False when a canceled record was re-provisioned.</param>
/// <param name="Applied">Whether the entitlement was provisioned by this request.</param>
/// <param name="AppliedReason">Why provisioning was not applied; null when the entitlement already exists and is not canceled.</param>
/// <param name="LastAppliedAt">The entitlement's <see cref="OrganizationPartnershipEntitlement.LastAppliedEffectiveDate"/>.</param>
public record ProvisionPartnershipEntitlementResult(
    OrganizationPartnershipEntitlement Entitlement,
    bool Created,
    bool Applied,
    string? AppliedReason,
    DateTime LastAppliedAt);
