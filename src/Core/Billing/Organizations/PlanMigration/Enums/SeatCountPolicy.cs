namespace Bit.Core.Billing.Organizations.PlanMigration.Enums;

/// <summary>
/// Determines how a <see cref="ValueObjects.MigrationPath"/> resolves the Phase 2 seat quantity.
/// </summary>
public enum SeatCountPolicy : byte
{
    /// <summary>
    /// Keep the source subscription's seat line-item quantity unchanged. The default for
    /// Scalable-to-Scalable migrations where the seat quantity carries over directly.
    /// </summary>
    Preserve = 0,

    /// <summary>
    /// Resolve the seat quantity from the organization's actual usage rather than the source
    /// line items. Used for Packaged plans (e.g. Teams 2019) whose flat base covers a block of
    /// seats and whose seat addon only counts seats beyond that base, so the source line items do
    /// not reflect the true total seat count on the Scalable target plan.
    /// </summary>
    ActualUsage = 1,
}