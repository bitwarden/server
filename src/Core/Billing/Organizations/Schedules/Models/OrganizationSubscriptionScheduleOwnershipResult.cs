using Bit.Core.Billing.Organizations.Schedules.Enums;
using Stripe;

namespace Bit.Core.Billing.Organizations.Schedules.Models;

/// <summary>
/// The ownership classification for a subscription's attached schedule, with the schedule itself
/// so callers can log or act on it without re-reading Stripe. <see cref="Schedule"/> is null when
/// there is nothing attached, and when the caller failed to expand it.
/// </summary>
public sealed record OrganizationSubscriptionScheduleOwnershipResult(
    OrganizationSubscriptionScheduleOwnership Ownership,
    SubscriptionSchedule? Schedule);
