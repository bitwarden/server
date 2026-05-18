CREATE TABLE [dbo].[OrganizationPlanMigrationCohort] (
    [Id]                          UNIQUEIDENTIFIER NOT NULL,
    [Name]                        NVARCHAR(255)   NOT NULL,
    [MigrationPathId]             TINYINT          NULL,
    [ProactiveDiscountCouponCode] NVARCHAR(64)    NULL,
    [ChurnDiscountCouponCode]     NVARCHAR(64)    NULL,
    [IsActive]                    BIT              NOT NULL CONSTRAINT [DF_OrganizationPlanMigrationCohort_IsActive] DEFAULT (0),
    [CreationDate]                DATETIME2(7)    NOT NULL,
    [RevisionDate]                DATETIME2(7)    NOT NULL,
    CONSTRAINT [PK_OrganizationPlanMigrationCohort] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [IX_OrganizationPlanMigrationCohort_Name] UNIQUE ([Name])
);
