using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Organizations.Helpers;

/// <summary>
/// Resolves the Privileged Controls seat minimum a new purchase bills: the requested promotional
/// minimum when it's on the approved list, otherwise the default. Shared so the purchase and its
/// preview stay aligned on the approved-list rule.
/// </summary>
public static class PrivilegedControlsSeatMinimumResolver
{
    /// <summary>
    /// Returns the seat minimum to bill. Callers confirm the plan supports Privileged Controls first.
    /// </summary>
    /// <param name="privilegedControls">The plan's Privileged Controls pricing, carrying the approved promotional minimums and the default.</param>
    /// <param name="requestedSeatMinimum">A promotional minimum from the trial link, accepted only if it's on the approved list.</param>
    public static int Resolve(Plan.PrivilegedControlsPlanFeatures privilegedControls, int? requestedSeatMinimum) =>
        requestedSeatMinimum is { } minimum &&
        privilegedControls.PromotionalSeatMinimums?.Contains(minimum) == true
            ? minimum
            : privilegedControls.DefaultSeatMinimum;
}
