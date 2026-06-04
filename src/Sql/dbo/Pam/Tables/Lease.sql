CREATE TABLE [dbo].[Lease] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [LeaseRequestId]    UNIQUEIDENTIFIER    NOT NULL,
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
    CONSTRAINT [PK_Lease] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Lease_LeaseRequest] FOREIGN KEY ([LeaseRequestId]) REFERENCES [dbo].[LeaseRequest] ([Id]),
    CONSTRAINT [FK_Lease_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_Lease_RequesterId_CipherId_Status]
    ON [dbo].[Lease] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Lease_NotAfter_Status]
    ON [dbo].[Lease] ([NotAfter] ASC, [Status] ASC);
GO
