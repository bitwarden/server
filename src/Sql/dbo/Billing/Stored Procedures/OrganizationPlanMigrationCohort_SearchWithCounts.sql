CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_SearchWithCounts]
    @Name NVARCHAR(255) = NULL,
    @Skip INT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COALESCE(SUM(CASE
            WHEN C.[MigrationPathId] IS NOT NULL AND A.[Id] IS NOT NULL AND A.[ScheduledDate] IS NULL THEN 1
            WHEN C.[MigrationPathId] IS NULL AND A.[Id] IS NOT NULL AND A.[ChurnDiscountAppliedDate] IS NULL THEN 1
            ELSE 0
        END), 0) AS [Pending],
        COALESCE(SUM(CASE
            WHEN C.[MigrationPathId] IS NOT NULL AND A.[ScheduledDate] IS NOT NULL AND A.[MigratedDate] IS NULL THEN 1
            ELSE 0
        END), 0) AS [Scheduled],
        COALESCE(SUM(CASE
            WHEN C.[MigrationPathId] IS NOT NULL AND A.[MigratedDate] IS NOT NULL THEN 1
            WHEN C.[MigrationPathId] IS NULL AND A.[ChurnDiscountAppliedDate] IS NOT NULL THEN 1
            ELSE 0
        END), 0) AS [Migrated],
        C.[Id],
        C.[Name],
        C.[MigrationPathId],
        C.[ProactiveDiscountCouponCode],
        C.[ChurnDiscountCouponCode],
        C.[IsActive],
        C.[CreationDate],
        C.[RevisionDate]
    FROM
        [dbo].[OrganizationPlanMigrationCohortView] C
        LEFT JOIN [dbo].[OrganizationPlanMigrationCohortAssignmentView] A
            ON A.[CohortId] = C.[Id]
    WHERE
        (@Name IS NULL OR C.[Name] LIKE '%' + @Name + '%')
    GROUP BY
        C.[Id],
        C.[Name],
        C.[MigrationPathId],
        C.[ProactiveDiscountCouponCode],
        C.[ChurnDiscountCouponCode],
        C.[IsActive],
        C.[CreationDate],
        C.[RevisionDate]
    ORDER BY
        C.[CreationDate] DESC, C.[Id] ASC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
END
