CREATE TABLE [dbo].[AccessLease] (
    [Id]                 UNIQUEIDENTIFIER    NOT NULL,
    [AccessRequestId]    UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]      UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]          UNIQUEIDENTIFIER    NOT NULL,
    [RequesterId]       UNIQUEIDENTIFIER    NOT NULL,
    [Status]            TINYINT             NOT NULL,
    [NotBefore]         DATETIME2 (7)       NOT NULL,
    [NotAfter]          DATETIME2 (7)       NOT NULL,
    [RevokedDate]       DATETIME2 (7)       NULL,
    [RevokedBy]         UNIQUEIDENTIFIER    NULL,
    [CreationDate]      DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_AccessLease] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessLease_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]),
    CONSTRAINT [FK_AccessLease_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessLease_RequesterId_CipherId_Status]
    ON [dbo].[AccessLease] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessLease_NotAfter_Status]
    ON [dbo].[AccessLease] ([NotAfter] ASC, [Status] ASC);
GO

-- Supports the governance lease lists (AccessLease_ReadManyActiveByCollectionIds /
-- AccessLease_ReadManyEndedByCollectionIds), which filter by the caller's manageable collection ids.
CREATE NONCLUSTERED INDEX [IX_AccessLease_CollectionId_Status]
    ON [dbo].[AccessLease] ([CollectionId] ASC, [Status] ASC);
GO

-- A request produces at most one lease, ever: activating an approved request and the automatic path each insert
-- exactly one. Unique to backstop racing activations that pass the application-level checks simultaneously.
CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessLease_AccessRequestId]
    ON [dbo].[AccessLease] ([AccessRequestId] ASC);
GO
