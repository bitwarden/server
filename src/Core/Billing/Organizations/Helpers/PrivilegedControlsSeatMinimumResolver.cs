using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Organizations.Helpers;

/// <summary>
/// Resolves the Privileged Controls seat minimum a new purchase bills: the requested promotional
/// minimum when it's on the approved list, otherwise the default. Shared so the purchase and its
/// preview stay aligned on the approved-list rule. Callers confirm the plan supports Privileged
/// Controls first.
/// </summary>
public static class PrivilegedControlsSeatMinimumResolver
{
    public static int Resolve(Plan.PrivilegedControlsPlanFeatures privilegedControls, int? requestedSeatMinimum) =>
        requestedSeatMinimum is { } minimum && privilegedControls.PromotionalSeatMinimums.Contains(minimum)
            ? minimum
            : privilegedControls.DefaultSeatMinimum;
}
