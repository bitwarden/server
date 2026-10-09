namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// The trial on an organization's trialing Stripe subscription.
/// </summary>
/// <param name="TrialEnd">When the trial ends (UTC).</param>
/// <param name="ExtensionBlockedReason">
/// Why the trial cannot be extended right now, per <see cref="TrialExtensionPolicy"/>; <see langword="null"/> when it can.
/// </param>
public record OrganizationTrial(DateTime TrialEnd, string? ExtensionBlockedReason)
{
    public bool CanExtend => ExtensionBlockedReason is null;
}
