CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadManyByExpirationDateRange]
    @MinDays INT,
    @MaxDays INT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE();

    SELECT
        CMA.*
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignment] CMA
        INNER JOIN
        [dbo].[OrganizationPlanMigrationCohort] C ON C.[Id] = CMA.[CohortId]
        INNER JOIN
        [dbo].[Organization] O ON O.[Id] = CMA.[OrganizationId]
    WHERE
        C.[IsActive] = 1
        AND CMA.[MigratedDate] IS NULL
        AND (CMA.[ScheduledDate] IS NULL OR CMA.[RenewalNotificationSentDate] IS NULL)
        AND O.[GatewayCustomerId] IS NOT NULL
        AND O.[GatewaySubscriptionId] IS NOT NULL
        AND O.[ExpirationDate] IS NOT NULL
        AND O.[ExpirationDate] >= DATEADD(DAY, @MinDays, @Now)
        AND O.[ExpirationDate] <= DATEADD(DAY, @MaxDays, @Now)
END
