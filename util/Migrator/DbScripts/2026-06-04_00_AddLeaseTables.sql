-- PAM Credential Leasing: LeaseRequest / Lease / LeaseDecision tables + procedures.

-- LeaseRequest (created first; the FK to Lease is added later, once Lease exists).
IF OBJECT_ID('[dbo].[LeaseRequest]') IS NULL
BEGIN
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
        CONSTRAINT [FK_LeaseRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_LeaseRequest_RequesterId_CipherId_Status' AND object_id = OBJECT_ID('[dbo].[LeaseRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaseRequest_RequesterId_CipherId_Status]
        ON [dbo].[LeaseRequest] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_LeaseRequest_OrganizationId_Status' AND object_id = OBJECT_ID('[dbo].[LeaseRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaseRequest_OrganizationId_Status]
        ON [dbo].[LeaseRequest] ([OrganizationId] ASC, [Status] ASC);
END
GO

-- Lease
IF OBJECT_ID('[dbo].[Lease]') IS NULL
BEGIN
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
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_Lease_RequesterId_CipherId_Status' AND object_id = OBJECT_ID('[dbo].[Lease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Lease_RequesterId_CipherId_Status]
        ON [dbo].[Lease] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_Lease_NotAfter_Status' AND object_id = OBJECT_ID('[dbo].[Lease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Lease_NotAfter_Status]
        ON [dbo].[Lease] ([NotAfter] ASC, [Status] ASC);
END
GO

-- Now that Lease exists, add the reciprocal FK from LeaseRequest.LeaseId (used by future extension requests).
IF OBJECT_ID('[dbo].[FK_LeaseRequest_Lease]', 'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[LeaseRequest]
        ADD CONSTRAINT [FK_LeaseRequest_Lease] FOREIGN KEY ([LeaseId]) REFERENCES [dbo].[Lease] ([Id]);
END
GO

-- LeaseDecision
IF OBJECT_ID('[dbo].[LeaseDecision]') IS NULL
BEGIN
    CREATE TABLE [dbo].[LeaseDecision] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [LeaseRequestId]        UNIQUEIDENTIFIER    NOT NULL,
        [DeciderKind]           TINYINT             NOT NULL,
        [ApproverId]            UNIQUEIDENTIFIER    NULL,
        [PolicyKind]            NVARCHAR(50)        NULL,
        [Decision]              TINYINT             NOT NULL,
        [Comment]               NVARCHAR(MAX)       NULL,
        [EvaluationContext]     NVARCHAR(MAX)       NULL,
        [CreationDate]          DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_LeaseDecision] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_LeaseDecision_LeaseRequest] FOREIGN KEY ([LeaseRequestId]) REFERENCES [dbo].[LeaseRequest] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_LeaseDecision_LeaseRequestId' AND object_id = OBJECT_ID('[dbo].[LeaseDecision]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaseDecision_LeaseRequestId]
        ON [dbo].[LeaseDecision] ([LeaseRequestId] ASC);
END
GO

-- Stored procedures
CREATE OR ALTER PROCEDURE [dbo].[LeaseRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @LeaseId UNIQUEIDENTIFIER = NULL,
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

    INSERT INTO [dbo].[LeaseRequest]
    (
        [Id], [LeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @Id, @LeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, @Status, @CreationDate, @ResolvedDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[LeaseRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT * FROM [dbo].[LeaseRequest] WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[LeaseRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT TOP 1 *
    FROM [dbo].[LeaseRequest]
    WHERE [RequesterId] = @RequesterId AND [CipherId] = @CipherId AND [Status] = 0 -- Pending
    ORDER BY [CreationDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Lease_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT * FROM [dbo].[Lease] WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Lease_ReadActiveByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SELECT TOP 1 *
    FROM [dbo].[Lease]
    WHERE [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Status] = 0 -- Active
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY [NotAfter] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Lease_CreateAutoApproved]
    @LeaseRequestId UNIQUEIDENTIFIER,
    @LeaseId UNIQUEIDENTIFIER,
    @LeaseDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @PolicyKind NVARCHAR(50) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION Lease_CreateAutoApproved

    INSERT INTO [dbo].[LeaseRequest]
    (
        [Id], [LeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @LeaseRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now
    )

    INSERT INTO [dbo].[LeaseDecision]
    (
        [Id], [LeaseRequestId], [DeciderKind], [ApproverId], [PolicyKind],
        [Decision], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @LeaseDecisionId, @LeaseRequestId, 0 /* Policy */, NULL, @PolicyKind,
        0 /* Approve */, NULL, NULL, @Now
    )

    INSERT INTO [dbo].[Lease]
    (
        [Id], [LeaseRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    VALUES
    (
        @LeaseId, @LeaseRequestId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        0 /* Active */, @NotBefore, @NotAfter, NULL, NULL, @Now
    )

    COMMIT TRANSACTION Lease_CreateAutoApproved
END
GO
