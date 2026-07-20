CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadManySendInvoiceCandidatesInDateRange]
    @MinDays INT,
    @MaxDays INT
AS
BEGIN
    SET NOCOUNT ON

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
        AND O.[ExpirationDate] >= DATEADD(DAY, @MinDays, GETUTCDATE())
        AND O.[ExpirationDate] <= DATEADD(DAY, @MaxDays, GETUTCDATE())
END
