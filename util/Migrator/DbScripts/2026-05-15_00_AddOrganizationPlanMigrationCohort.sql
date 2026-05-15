-- Tables
-- ON DELETE CASCADE is intentional on both foreign keys: a deleted Organization should not
-- block deletion or leave orphaned assignment rows, and a deleted Cohort should drop its
-- assignment rows with it (cohorts and their assignments share a lifecycle).
IF OBJECT_ID('[dbo].[OrganizationPlanMigrationCohort]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationPlanMigrationCohort]
    (
        [Id]                          UNIQUEIDENTIFIER NOT NULL,
        [Name]                        NVARCHAR (255)   NOT NULL,
        [MigrationPathId]             TINYINT          NULL,
        [ProactiveDiscountCouponCode] NVARCHAR (64)    NULL,
        [ChurnDiscountCouponCode]     NVARCHAR (64)    NULL,
        [IsActive]                    BIT              NOT NULL CONSTRAINT [DF_OrganizationPlanMigrationCohort_IsActive] DEFAULT (0),
        [CreatedAt]                   DATETIME2 (7)    NOT NULL,
        [RevisionDate]                DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_OrganizationPlanMigrationCohort] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [IX_OrganizationPlanMigrationCohort_Name] UNIQUE ([Name])
    );
END
GO

IF OBJECT_ID('[dbo].[OrganizationPlanMigrationCohortAssignment]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationPlanMigrationCohortAssignment]
    (
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
END
GO

-- Composite index supports the Pending/Scheduled/Migrated counts query consumed by
-- the Cohort Management aggregate (PM-36951). Land it here so the consumer does not
-- need a follow-up ALTER migration.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledAt_MigratedAt'
      AND object_id = OBJECT_ID('[dbo].[OrganizationPlanMigrationCohortAssignment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledAt_MigratedAt]
        ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [ScheduledAt] ASC, [MigratedAt] ASC);
END
GO

-- Views
CREATE OR ALTER VIEW [dbo].[OrganizationPlanMigrationCohortView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationPlanMigrationCohort]
GO

CREATE OR ALTER VIEW [dbo].[OrganizationPlanMigrationCohortAssignmentView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationPlanMigrationCohortAssignment]
GO

-- Stored Procedures: OrganizationPlanMigrationCohort
CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_Update]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohort_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPlanMigrationCohort]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: OrganizationPlanMigrationCohortAssignment
CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CohortId UNIQUEIDENTIFIER,
    @ScheduledAt DATETIME2 (7),
    @MigratedAt DATETIME2 (7),
    @ChurnDiscountAppliedAt DATETIME2 (7),
    @CreatedAt DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPlanMigrationCohortAssignment]
    (
        [Id],
        [OrganizationId],
        [CohortId],
        [ScheduledAt],
        [MigratedAt],
        [ChurnDiscountAppliedAt],
        [CreatedAt],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CohortId,
        @ScheduledAt,
        @MigratedAt,
        @ChurnDiscountAppliedAt,
        @CreatedAt,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignmentView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CohortId UNIQUEIDENTIFIER,
    @ScheduledAt DATETIME2 (7),
    @MigratedAt DATETIME2 (7),
    @ChurnDiscountAppliedAt DATETIME2 (7),
    @CreatedAt DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    -- @OrganizationId, @CohortId, and @CreatedAt are accepted but not assigned; they are
    -- immutable once the row is inserted. The assignment-to-cohort and assignment-to-org
    -- relationships cannot be changed after creation -- create a new row instead.
    UPDATE
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    SET
        [ScheduledAt] = @ScheduledAt,
        [MigratedAt] = @MigratedAt,
        [ChurnDiscountAppliedAt] = @ChurnDiscountAppliedAt,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignmentView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
