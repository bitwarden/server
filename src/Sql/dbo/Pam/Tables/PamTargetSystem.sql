-- Automatic (connector-driven) or Manual (person-driven) target. SealedData is an opaque blob the admin client
-- seals under the org key (kind, password policy, session-termination capability); the server never parses it, and
-- it is null while Status is PendingSeal (registered but not yet sealed).
CREATE TABLE [dbo].[PamTargetSystem] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
    [Name]                          NVARCHAR(200)        NOT NULL,
    [Method]                        TINYINT              NOT NULL,
    [Status]                        TINYINT              NOT NULL,
    [CreationDate]                  DATETIME2(7)         NOT NULL,
    [RevisionDate]                  DATETIME2(7)         NOT NULL,
    [SealedData]                    NVARCHAR(MAX)        NULL,
    CONSTRAINT [PK_PamTargetSystem] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamTargetSystem_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

-- Backs PamTargetSystem_ReadByOrganizationId and the Organization cascade delete.
CREATE NONCLUSTERED INDEX [IX_PamTargetSystem_OrganizationId]
    ON [dbo].[PamTargetSystem] ([OrganizationId] ASC);
GO
