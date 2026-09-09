using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.PlanMigration;

public static class PlanMigrationExtensions
{
    /// <summary>
    /// Whether a migration source should be treated as Packaged — a flat bundle, or any source whose
    /// line items don't reflect the true seat total (<see cref="SeatCountPolicy.ActualUsage"/>).
    /// </summary>
    /// <param name="sourcePlan">The migration source plan.</param>
    /// <param name="policy">The migration path's seat-count policy.</param>
    public static bool IsPackagedMigrationSource(this Plan sourcePlan, SeatCountPolicy policy) =>
        sourcePlan.HasNonSeatBasedPasswordManagerPlan() || policy == SeatCountPolicy.ActualUsage;

    /// <summary>
    /// Resolves the billed seat count for a Packaged migration source: occupied seats for a flat
    /// bundle (floored at 1), or occupied-below-base / purchased-at-or-above-base for seat overage.
    /// </summary>
    /// <param name="sourcePlan">The Packaged source plan being migrated.</param>
    /// <param name="occupiedSeats">The organization's current occupied seat total.</param>
    /// <param name="purchasedSeats">The organization's purchased seat count, billed at or above the base.</param>
    /// <exception cref="ArgumentException">The plan is a Scalable source, which must preserve its line-item quantity.</exception>
    public static int ResolveMigratedSeatCount(this Plan sourcePlan, int occupiedSeats, int? purchasedSeats)
    {
        ArgumentNullException.ThrowIfNull(sourcePlan);
        ArgumentOutOfRangeException.ThrowIfNegative(occupiedSeats);

        // Flat bundle (e.g. Teams Starter): no per-seat line — bill occupied seats, floored at 1.
        if (sourcePlan.HasNonSeatBasedPasswordManagerPlan())
        {
            return Math.Max(1, occupiedSeats);
        }

        // Seat overage (e.g. Teams 2019) needs a packaged base. A Scalable source has none
        // (BaseSeats == 0) and must preserve its line-item quantity, not resolve from usage here.
        if (sourcePlan.PasswordManager.BaseSeats <= 0)
        {
            throw new ArgumentException(
                $"{nameof(ResolveMigratedSeatCount)} supports only Packaged sources; " +
                $"'{sourcePlan.Name}' has no packaged base and must preserve its line-item quantity.",
                nameof(sourcePlan));
        }

        return occupiedSeats < sourcePlan.PasswordManager.BaseSeats
            ? occupiedSeats
            : purchasedSeats ?? occupiedSeats;
    }
}
