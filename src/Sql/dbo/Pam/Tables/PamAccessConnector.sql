-- Credential lives on the shared dbo.ApiKey store (ApiKeyId, unique), not duplicated here.
-- "Connected" is derived from LastHeartbeatAt; there's no separate state table.
CREATE TABLE [dbo].[PamAccessConnector] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [Name]              NVARCHAR(200)       NOT NULL,
    [ApiKeyId]          UNIQUEIDENTIFIER    NOT NULL,
    [Status]            TINYINT             NOT NULL,
    [LastHeartbeatAt]   DATETIME2(7)        NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    [RevisionDate]      DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamAccessConnector] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamAccessConnector_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    -- No cascade, since deleting the credential first would remove the access connector; both delete together.
    CONSTRAINT [FK_PamAccessConnector_ApiKey] FOREIGN KEY ([ApiKeyId]) REFERENCES [dbo].[ApiKey] ([Id]) ON DELETE NO ACTION
);
GO

-- One credential per access connector; also the lookup PamAccessConnectorDetails_ReadByApiKeyId uses at token time.
CREATE UNIQUE NONCLUSTERED INDEX [IX_PamAccessConnector_ApiKeyId]
    ON [dbo].[PamAccessConnector] ([ApiKeyId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_PamAccessConnector_OrganizationId]
    ON [dbo].[PamAccessConnector] ([OrganizationId] ASC);
GO
