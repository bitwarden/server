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
