-- Automatic (connector-driven) or Manual (person-driven, Kind/SupportsSessionTermination null).
-- PasswordPolicy is JSON the access connector generates passwords against; optional on a Manual target.
CREATE TABLE [dbo].[PamTargetSystem] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
    [Name]                          NVARCHAR(200)        NOT NULL,
    [Method]                        TINYINT              NOT NULL,
    [Kind]                          TINYINT              NULL,
    [PasswordPolicy]                NVARCHAR(2000)       NULL,
    [SupportsSessionTermination]    BIT                  NULL,
    [Status]                        TINYINT              NOT NULL,
    [CreationDate]                  DATETIME2(7)         NOT NULL,
    [RevisionDate]                  DATETIME2(7)         NOT NULL,
    CONSTRAINT [PK_PamTargetSystem] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamTargetSystem_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

-- Backs PamTargetSystem_ReadByOrganizationId and the Organization cascade delete.
CREATE NONCLUSTERED INDEX [IX_PamTargetSystem_OrganizationId]
    ON [dbo].[PamTargetSystem] ([OrganizationId] ASC);
GO
