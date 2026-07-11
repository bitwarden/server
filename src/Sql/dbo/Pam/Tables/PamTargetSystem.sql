-- A target system is where a managed credential's password is rotated: either automatically by a daemon (Method =
-- Automatic, Kind identifies which connector) or manually by a person (Method = Manual, Kind/PasswordPolicy null --
-- there is no connector to configure). PasswordPolicy is opaque JSON (the PasswordPolicy value object) the daemon
-- applies when generating a new secret; the server never inspects it.
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
