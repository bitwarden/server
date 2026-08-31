-- A registered rotation daemon: the credential itself lives on the shared dbo.ApiKey machine-credential store
-- (ApiKeyId, unique -- one credential per daemon), not duplicated here. LastHeartbeatAt is bumped on every daemon
-- poll; there is no separate connection-state table -- "connected" is derived (LastHeartbeatAt within
-- DaemonOfflineAfter of now).
CREATE TABLE [dbo].[PamDaemon] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [Name]              NVARCHAR(200)       NOT NULL,
    [ApiKeyId]          UNIQUEIDENTIFIER    NOT NULL,
    [Status]            TINYINT             NOT NULL,
    [LastHeartbeatAt]   DATETIME2(7)        NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    [RevisionDate]      DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamDaemon] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamDaemon_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    -- No cascade: it would fire the wrong way round, deleting the daemon when its credential goes.
    -- PamDaemon_DeleteById deletes both rows, daemon first, in one transaction.
    CONSTRAINT [FK_PamDaemon_ApiKey] FOREIGN KEY ([ApiKeyId]) REFERENCES [dbo].[ApiKey] ([Id]) ON DELETE NO ACTION
);
GO

-- OneKeyPerDaemon; also the lookup path PamDaemonDetails_ReadByApiKeyId uses at token time.
CREATE UNIQUE NONCLUSTERED INDEX [IX_PamDaemon_ApiKeyId]
    ON [dbo].[PamDaemon] ([ApiKeyId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_PamDaemon_OrganizationId]
    ON [dbo].[PamDaemon] ([OrganizationId] ASC);
GO
