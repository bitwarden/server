CREATE TABLE [dbo].[AccessRequest] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [ExtensionOfLeaseId]    UNIQUEIDENTIFIER    NULL,
    [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]          UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]              UNIQUEIDENTIFIER    NOT NULL,
    [RequesterId]           UNIQUEIDENTIFIER    NOT NULL,
    [NotBefore]             DATETIME2 (7)       NOT NULL,
    [NotAfter]              DATETIME2 (7)       NOT NULL,
    [Reason]                NVARCHAR(MAX)       NULL,
    [Status]                TINYINT             NOT NULL,
    [CreationDate]          DATETIME2 (7)       NOT NULL,
    [ResolvedDate]          DATETIME2 (7)       NULL,
    CONSTRAINT [PK_AccessRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessRequest_AccessLease] FOREIGN KEY ([ExtensionOfLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]),
    CONSTRAINT [FK_AccessRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Status]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_OrganizationId_Status]
    ON [dbo].[AccessRequest] ([OrganizationId] ASC, [Status] ASC);
GO
