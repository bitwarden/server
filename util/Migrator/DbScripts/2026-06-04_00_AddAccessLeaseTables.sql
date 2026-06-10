-- PAM Credential Leasing: AccessRequest / AccessLease / AccessDecision tables + procedures.

-- AccessRequest (created first; the FK to AccessLease is added later, once AccessLease exists).
IF OBJECT_ID('[dbo].[AccessRequest]') IS NULL
BEGIN
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
        CONSTRAINT [FK_AccessRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_AccessRequest_RequesterId_CipherId_Status' AND object_id = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Status]
        ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_AccessRequest_OrganizationId_Status' AND object_id = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_OrganizationId_Status]
        ON [dbo].[AccessRequest] ([OrganizationId] ASC, [Status] ASC);
END
GO

-- AccessLease
IF OBJECT_ID('[dbo].[AccessLease]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessLease] (
        [Id]                 UNIQUEIDENTIFIER    NOT NULL,
        [AccessRequestId]    UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER    NOT NULL,
        [CollectionId]       UNIQUEIDENTIFIER    NOT NULL,
        [CipherId]           UNIQUEIDENTIFIER    NOT NULL,
        [RequesterId]        UNIQUEIDENTIFIER    NOT NULL,
        [Status]             TINYINT             NOT NULL,
        [NotBefore]          DATETIME2 (7)       NOT NULL,
        [NotAfter]           DATETIME2 (7)       NOT NULL,
        [RevokedDate]        DATETIME2 (7)       NULL,
        [RevokedBy]          UNIQUEIDENTIFIER    NULL,
        [CreationDate]       DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_AccessLease] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessLease_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]),
        CONSTRAINT [FK_AccessLease_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_AccessLease_RequesterId_CipherId_Status' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_RequesterId_CipherId_Status]
        ON [dbo].[AccessLease] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_AccessLease_NotAfter_Status' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_NotAfter_Status]
        ON [dbo].[AccessLease] ([NotAfter] ASC, [Status] ASC);
END
GO

-- Now that AccessLease exists, add the reciprocal FK from AccessRequest.ExtensionOfLeaseId (used by future extension requests).
IF OBJECT_ID('[dbo].[FK_AccessRequest_AccessLease]', 'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequest]
        ADD CONSTRAINT [FK_AccessRequest_AccessLease] FOREIGN KEY ([ExtensionOfLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]);
END
GO

-- AccessDecision
IF OBJECT_ID('[dbo].[AccessDecision]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessDecision] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [AccessRequestId]       UNIQUEIDENTIFIER    NOT NULL,
        [DeciderKind]           TINYINT             NOT NULL,
        [ApproverId]            UNIQUEIDENTIFIER    NULL,
        [ConditionKind]         NVARCHAR(50)        NULL,
        [Verdict]               TINYINT             NOT NULL,
        [Comment]               NVARCHAR(MAX)       NULL,
        [EvaluationContext]     NVARCHAR(MAX)       NULL,
        [CreationDate]          DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_AccessDecision] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessDecision_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_AccessDecision_AccessRequestId' AND object_id = OBJECT_ID('[dbo].[AccessDecision]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessDecision_AccessRequestId]
        ON [dbo].[AccessDecision] ([AccessRequestId] ASC);
END
GO

-- Stored procedures
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER = NULL,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @ResolvedDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @Id, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, @Status, @CreationDate, @ResolvedDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT * FROM [dbo].[AccessRequest] WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT TOP 1 *
    FROM [dbo].[AccessRequest]
    WHERE [RequesterId] = @RequesterId AND [CipherId] = @CipherId AND [Status] = 0 -- Pending
    ORDER BY [CreationDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT * FROM [dbo].[AccessLease] WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadActiveByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SELECT TOP 1 *
    FROM [dbo].[AccessLease]
    WHERE [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Status] = 0 -- Active
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY [NotAfter] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateAutoApproved]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @ConditionKind NVARCHAR(50) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION AccessLease_CreateAutoApproved

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AccessRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, @ConditionKind,
        0 /* Approve */, NULL, NULL, @Now
    )

    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    VALUES
    (
        @AccessLeaseId, @AccessRequestId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        0 /* Active */, @NotBefore, @NotAfter, NULL, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_CreateAutoApproved
END
GO
