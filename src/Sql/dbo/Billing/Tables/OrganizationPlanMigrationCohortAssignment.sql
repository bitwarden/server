CREATE TABLE [dbo].[OrganizationPlanMigrationCohortAssignment] (
    [Id]                            UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER NOT NULL,
    [CohortId]                      UNIQUEIDENTIFIER NOT NULL,
    [ScheduledDate]                 DATETIME2(7)     NULL,
    [MigratedDate]                  DATETIME2(7)     NULL,
    [ChurnDiscountAppliedDate]      DATETIME2(7)     NULL,
    [CreationDate]                  DATETIME2(7)     NOT NULL,
    [RevisionDate]                  DATETIME2(7)     NOT NULL,
    [RenewalNotificationSentDate]   DATETIME2(7)     NULL,
    CONSTRAINT [PK_OrganizationPlanMigrationCohortAssignment] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationPlanMigrationCohortAssignment_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrganizationPlanMigrationCohortAssignment_Cohort] FOREIGN KEY ([CohortId]) REFERENCES [dbo].[OrganizationPlanMigrationCohort] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [IX_OrganizationPlanMigrationCohortAssignment_OrganizationId] UNIQUE ([OrganizationId])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledDate_MigratedDate]
    ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [ScheduledDate] ASC, [MigratedDate] ASC)
    INCLUDE ([ChurnDiscountAppliedDate]);
GO

-- Serves the CSV export cursor: cohort filter plus the (CreationDate, Id) keyset seek used by
-- OrganizationPlanMigrationCohortAssignment_ReadManyExportByCohortId.
CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_CreationDate_Id]
    ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [CreationDate] ASC, [Id] ASC);
