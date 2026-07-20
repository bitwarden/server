-- Add RenewalNotificationSentDate column to OrganizationPlanMigrationCohortAssignment
IF COL_LENGTH('[dbo].[OrganizationPlanMigrationCohortAssignment]', 'RenewalNotificationSentDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationPlanMigrationCohortAssignment]
        ADD [RenewalNotificationSentDate] DATETIME2(7) NULL;
END
GO

-- Backfill RenewalNotificationSentDate = ScheduledDate for rows already processed via the
-- charge_automatically path, so they read as notified rather than incomplete.
UPDATE [dbo].[OrganizationPlanMigrationCohortAssignment]
SET [RenewalNotificationSentDate] = [ScheduledDate]
WHERE [ScheduledDate] IS NOT NULL
    AND [RenewalNotificationSentDate] IS NULL;
GO

-- Add RenewalNotificationSentDate to the Create stored procedure
CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CohortId UNIQUEIDENTIFIER,
    @ScheduledDate DATETIME2(7),
    @MigratedDate DATETIME2(7),
    @ChurnDiscountAppliedDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RenewalNotificationSentDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPlanMigrationCohortAssignment]
    (
        [Id],
        [OrganizationId],
        [CohortId],
        [ScheduledDate],
        [MigratedDate],
        [ChurnDiscountAppliedDate],
        [CreationDate],
        [RevisionDate],
        [RenewalNotificationSentDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CohortId,
        @ScheduledDate,
        @MigratedDate,
        @ChurnDiscountAppliedDate,
        @CreationDate,
        @RevisionDate,
        @RenewalNotificationSentDate
    )
END
GO

-- Add RenewalNotificationSentDate to the Update stored procedure
CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CohortId UNIQUEIDENTIFIER,
    @ScheduledDate DATETIME2(7),
    @MigratedDate DATETIME2(7),
    @ChurnDiscountAppliedDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RenewalNotificationSentDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @OrganizationId, @CohortId, and @CreationDate are accepted but not assigned; they are
    -- immutable once the row is inserted. The assignment-to-cohort and assignment-to-org
    -- relationships cannot be changed after creation -- create a new row instead.
    UPDATE
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    SET
        [ScheduledDate] = @ScheduledDate,
        [MigratedDate] = @MigratedDate,
        [ChurnDiscountAppliedDate] = @ChurnDiscountAppliedDate,
        [RevisionDate] = @RevisionDate,
        [RenewalNotificationSentDate] = @RenewalNotificationSentDate
    WHERE
        [Id] = @Id
END
GO

-- Add ReadManySendInvoiceCandidatesInDateRange stored procedure
CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadManySendInvoiceCandidatesInDateRange]
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
GO
