CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_TryClaimChurnDiscount]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomic compare-and-set: stamp ChurnDiscountAppliedDate only when currently NULL.
    -- The conditional predicate is the only post-consumption defense for once-duration
    -- churn coupons (the coupon falls off subscription.discounts after the first invoice).
    -- Callers inspect @@ROWCOUNT: 1 = won the race, 0 = already claimed.
    UPDATE
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    SET
        [ChurnDiscountAppliedDate] = @Now,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
        AND [ChurnDiscountAppliedDate] IS NULL
END
