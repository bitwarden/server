CREATE TABLE [dbo].[LeaseRequest] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [LeaseId]           UNIQUEIDENTIFIER    NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]      UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]          UNIQUEIDENTIFIER    NOT NULL,
    [RequesterId]       UNIQUEIDENTIFIER    NOT NULL,
    [NotBefore]         DATETIME2 (7)       NOT NULL,
    [NotAfter]          DATETIME2 (7)       NOT NULL,
    [Reason]            NVARCHAR(MAX)       NULL,
    [Status]            TINYINT             NOT NULL,
    [CreationDate]      DATETIME2 (7)       NOT NULL,
    [ResolvedDate]      DATETIME2 (7)       NULL,
    CONSTRAINT [PK_LeaseRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_LeaseRequest_Lease] FOREIGN KEY ([LeaseId]) REFERENCES [dbo].[Lease] ([Id]),
    CONSTRAINT [FK_LeaseRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_LeaseRequest_RequesterId_CipherId_Status]
    ON [dbo].[LeaseRequest] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_LeaseRequest_OrganizationId_Status]
    ON [dbo].[LeaseRequest] ([OrganizationId] ASC, [Status] ASC);
GO
