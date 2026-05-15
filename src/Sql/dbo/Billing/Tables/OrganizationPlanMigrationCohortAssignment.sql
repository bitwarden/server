CREATE TABLE [dbo].[OrganizationPlanMigrationCohortAssignment] (
    [Id]                     UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]         UNIQUEIDENTIFIER NOT NULL,
    [CohortId]               UNIQUEIDENTIFIER NOT NULL,
    [ScheduledAt]            DATETIME2 (7)    NULL,
    [MigratedAt]             DATETIME2 (7)    NULL,
    [ChurnDiscountAppliedAt] DATETIME2 (7)    NULL,
    [CreatedAt]              DATETIME2 (7)    NOT NULL,
    [RevisionDate]           DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrganizationPlanMigrationCohortAssignment] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationPlanMigrationCohortAssignment_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrganizationPlanMigrationCohortAssignment_Cohort] FOREIGN KEY ([CohortId]) REFERENCES [dbo].[OrganizationPlanMigrationCohort] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [IX_OrganizationPlanMigrationCohortAssignment_OrganizationId] UNIQUE ([OrganizationId])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledAt_MigratedAt]
    ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [ScheduledAt] ASC, [MigratedAt] ASC);
