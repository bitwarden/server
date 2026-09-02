CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadManyWithCountsByName]
    @Name NVARCHAR(255) = NULL,
    @Skip INT,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [PagedCohorts] AS (
        SELECT
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
        WHERE
            (@Name IS NULL OR C.[Name] LIKE '%' + @Name + '%')
        ORDER BY
            C.[CreationDate] DESC,
            C.[Id] ASC
        OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
    )
    SELECT
        COUNT(CASE
            WHEN P.[MigrationPathId] IS NOT NULL AND A.[Id] IS NOT NULL AND A.[ScheduledDate] IS NULL THEN 1
            WHEN P.[MigrationPathId] IS NULL AND A.[Id] IS NOT NULL AND A.[ChurnDiscountAppliedDate] IS NULL THEN 1
        END) AS [Pending],
        COUNT(CASE
            WHEN P.[MigrationPathId] IS NOT NULL AND A.[ScheduledDate] IS NOT NULL AND A.[MigratedDate] IS NULL THEN 1
        END) AS [Scheduled],
        COUNT(CASE
            WHEN P.[MigrationPathId] IS NOT NULL AND A.[MigratedDate] IS NOT NULL THEN 1
            WHEN P.[MigrationPathId] IS NULL AND A.[ChurnDiscountAppliedDate] IS NOT NULL THEN 1
        END) AS [Migrated],
        P.[Id],
        P.[Name],
        P.[MigrationPathId],
        P.[ProactiveDiscountCouponCode],
        P.[ChurnDiscountCouponCode],
        P.[IsActive],
        P.[CreationDate],
        P.[RevisionDate]
    FROM
        [PagedCohorts] P
    LEFT JOIN [dbo].[OrganizationPlanMigrationCohortAssignmentView] A
        ON A.[CohortId] = P.[Id]
    GROUP BY
        P.[Id],
        P.[Name],
        P.[MigrationPathId],
        P.[ProactiveDiscountCouponCode],
        P.[ChurnDiscountCouponCode],
        P.[IsActive],
        P.[CreationDate],
        P.[RevisionDate]
    ORDER BY
        P.[CreationDate] DESC,
        P.[Id] ASC
END
GO
