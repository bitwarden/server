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
    -- Action taken on the request; Pending/Expired are derived, not stored here.
    [Action]                TINYINT             NOT NULL,
    [CreationDate]          DATETIME2 (7)       NOT NULL,
    [ActionDate]            DATETIME2 (7)       NULL,
    [RuleId]                UNIQUEIDENTIFIER    NULL,
    CONSTRAINT [PK_AccessRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessRequest_AccessLease] FOREIGN KEY ([ExtensionOfLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]),
    CONSTRAINT [FK_AccessRequest_AccessRule] FOREIGN KEY ([RuleId]) REFERENCES [dbo].[AccessRule] ([Id]),
    CONSTRAINT [FK_AccessRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Action]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Action] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_OrganizationId_Action]
    ON [dbo].[AccessRequest] ([OrganizationId] ASC, [Action] ASC);
GO

-- Supports the approver's pending inbox; [NotAfter] lets the clock check seek, not scan.
-- Mirrors IX_AccessLease_CollectionId_Action's access pattern.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_Action_NotAfter]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [Action] ASC, [NotAfter] ASC);
GO

-- Supports the approver history read; [CreationDate] leads since the OR can't seek.
-- [Action]/[NotAfter] are INCLUDEd to settle the OR before the clustered lookup.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_CreationDate]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [CreationDate] ASC)
    INCLUDE ([Action], [NotAfter]);
GO

-- Supports the requester's TOP(250) history page via a backward scan on [CreationDate].
-- [Action]/[NotAfter] are INCLUDEd to settle the live-row check first.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CreationDate]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CreationDate] ASC)
    INCLUDE ([Action], [NotAfter]);
GO

-- Supports the extension cap checks and indexes FK_AccessRequest_AccessLease.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_ExtensionOfLeaseId]
    ON [dbo].[AccessRequest] ([ExtensionOfLeaseId] ASC);
GO

-- Indexes FK_AccessRequest_AccessRule so AccessRule_DeleteById can detach requests with a seek.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_RuleId]
    ON [dbo].[AccessRequest] ([RuleId] ASC);
GO
