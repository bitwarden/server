namespace Bit.Core.Billing.Organizations.PlanMigration.Enums;

/// <summary>
/// Determines how a <see cref="ValueObjects.MigrationPath"/> resolves the Phase 2 seat quantity.
/// </summary>
public enum SeatCountPolicy : byte
{
    /// <summary>
    /// Keep the source subscription's seat line-item quantity unchanged (Scalable-to-Scalable).
    /// </summary>
    Preserve = 0,

    /// <summary>
    /// Resolve the seat quantity from the organization's actual usage rather than the source line
    /// items, whose quantities don't reflect the true total on a Packaged source
    /// </summary>
    ActualUsage = 1,
}
