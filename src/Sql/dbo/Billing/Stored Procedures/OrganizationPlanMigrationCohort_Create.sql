CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Name NVARCHAR(64),
    @MigrationPathId TINYINT,
    @ProactiveDiscountCouponCode NVARCHAR(64),
    @ChurnDiscountCouponCode NVARCHAR(64),
    @IsActive BIT,
    @CreatedAt DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPlanMigrationCohort]
    (
        [Id],
        [Name],
        [MigrationPathId],
        [ProactiveDiscountCouponCode],
        [ChurnDiscountCouponCode],
        [IsActive],
        [CreatedAt],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Name,
        @MigrationPathId,
        @ProactiveDiscountCouponCode,
        @ChurnDiscountCouponCode,
        @IsActive,
        @CreatedAt,
        @RevisionDate
    )
END
