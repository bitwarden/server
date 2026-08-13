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
    [RuleId]                UNIQUEIDENTIFIER    NULL,
    CONSTRAINT [PK_AccessRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessRequest_AccessLease] FOREIGN KEY ([ExtensionOfLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]),
    CONSTRAINT [FK_AccessRequest_AccessRule] FOREIGN KEY ([RuleId]) REFERENCES [dbo].[AccessRule] ([Id]),
    CONSTRAINT [FK_AccessRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Status]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_OrganizationId_Status]
    ON [dbo].[AccessRequest] ([OrganizationId] ASC, [Status] ASC);
GO

-- Supports the approver inbox reads (AccessRequest_ReadInboxPendingByCollectionIds /
-- AccessRequest_ReadInboxHistoryByCollectionIds), which join the caller's manageable collection ids and filter on
-- status. Mirrors IX_AccessLease_CollectionId_Status for the same governance-by-collection access pattern.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_Status]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [Status] ASC);
GO

-- Supports the extension cap checks (AccessRequest_CreateApprovedExtension's EXISTS, which runs while holding the
-- parent lease's UPDLOCK, and AccessRequest_CountExtensionsByLeaseId), and indexes FK_AccessRequest_AccessLease.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_ExtensionOfLeaseId]
    ON [dbo].[AccessRequest] ([ExtensionOfLeaseId] ASC);
GO

-- Indexes FK_AccessRequest_AccessRule so AccessRule_DeleteById can detach the requests pinning a rule with a seek
-- rather than a scan.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_RuleId]
    ON [dbo].[AccessRequest] ([RuleId] ASC);
GO
