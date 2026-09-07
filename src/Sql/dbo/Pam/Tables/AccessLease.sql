CREATE TABLE [dbo].[AccessLease] (
    [Id]                 UNIQUEIDENTIFIER    NOT NULL,
    [AccessRequestId]    UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]      UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]          UNIQUEIDENTIFIER    NOT NULL,
    [RequesterId]       UNIQUEIDENTIFIER    NOT NULL,
    -- How the lease ended early (0 None, 2 Revoked, 3 Cancelled); Active/Expired are derived.
    [Action]            TINYINT             NOT NULL,
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

CREATE NONCLUSTERED INDEX [IX_AccessLease_RequesterId_CipherId_Action]
    ON [dbo].[AccessLease] ([RequesterId] ASC, [CipherId] ASC, [Action] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessLease_NotAfter_Action]
    ON [dbo].[AccessLease] ([NotAfter] ASC, [Action] ASC);
GO

-- Supports the governance lease lists filtered by the caller's manageable collection ids.
CREATE NONCLUSTERED INDEX [IX_AccessLease_CollectionId_Action]
    ON [dbo].[AccessLease] ([CollectionId] ASC, [Action] ASC);
GO

-- Supports the per-cipher singleton guard, which locks by CipherId alone.
-- NotAfter DESC lets AccessLease_ReadActiveByCipherId seek in-window rows without a sort.
CREATE NONCLUSTERED INDEX [IX_AccessLease_CipherId_Action]
    ON [dbo].[AccessLease] ([CipherId] ASC, [Action] ASC, [NotAfter] DESC);
GO

-- A request produces at most one lease, ever; unique index backstops racing activations.
CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessLease_AccessRequestId]
    ON [dbo].[AccessLease] ([AccessRequestId] ASC);
GO
