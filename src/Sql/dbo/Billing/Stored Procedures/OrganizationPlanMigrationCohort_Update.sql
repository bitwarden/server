CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(255),
    @MigrationPathId TINYINT,
    @ProactiveDiscountCouponCode NVARCHAR(64),
    @ChurnDiscountCouponCode NVARCHAR(64),
    @IsActive BIT,
    @CreatedAt DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    -- @CreatedAt is accepted but not assigned; it is immutable once the row is inserted.
    UPDATE
        [dbo].[OrganizationPlanMigrationCohort]
    SET
        [Name] = @Name,
        [MigrationPathId] = @MigrationPathId,
        [ProactiveDiscountCouponCode] = @ProactiveDiscountCouponCode,
        [ChurnDiscountCouponCode] = @ChurnDiscountCouponCode,
        [IsActive] = @IsActive,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
