namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// The result of validating a Privileged Controls seat request: the change set that applies it, and the seat
/// minimum the request was validated against.
/// </summary>
/// <param name="ChangeSet">The change set to apply through <see cref="Commands.IUpdateOrganizationSubscriptionCommand"/>.</param>
/// <param name="SeatMinimum">
/// The organization's saved minimum, or the plan's default when none is saved yet. A caller making a first
/// purchase saves this on the organization.
/// </param>
public record PrivilegedControlsSeatChange(OrganizationSubscriptionChangeSet ChangeSet, int SeatMinimum);
