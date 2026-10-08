-- Table
IF OBJECT_ID('[dbo].[AgentFillApprovalRequest]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AgentFillApprovalRequest]
    (
        [Id]               UNIQUEIDENTIFIER NOT NULL,
        [UserId]           UNIQUEIDENTIFIER NOT NULL,
        [RequestDeviceId]  UNIQUEIDENTIFIER NOT NULL,
        [SealedRequest]    VARCHAR(MAX)     NOT NULL,
        [SealedResponse]   VARCHAR(MAX)     NULL,
        [ResponseDeviceId] UNIQUEIDENTIFIER NULL,
        [CreationDate]     DATETIME2(7)     NOT NULL,
        [ExpirationDate]   DATETIME2(7)     NOT NULL,
        [ResponseDate]     DATETIME2(7)     NULL,
        CONSTRAINT [PK_AgentFillApprovalRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AgentFillApprovalRequest_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AgentFillApprovalRequest_Device] FOREIGN KEY ([RequestDeviceId]) REFERENCES [dbo].[Device] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AgentFillApprovalRequest_ExpirationDate]
        ON [dbo].[AgentFillApprovalRequest]([ExpirationDate] ASC);
END
GO

-- Stored Procedures
CREATE OR ALTER PROCEDURE [dbo].[AgentFillApprovalRequest_Create]
    @Id               UNIQUEIDENTIFIER,
    @UserId           UNIQUEIDENTIFIER,
    @RequestDeviceId  UNIQUEIDENTIFIER,
    @SealedRequest    VARCHAR(MAX),
    @CreationDate     DATETIME2(7),
    @ExpirationDate   DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AgentFillApprovalRequest]
    (
        [Id],
        [UserId],
        [RequestDeviceId],
        [SealedRequest],
        [SealedResponse],
        [ResponseDeviceId],
        [CreationDate],
        [ExpirationDate],
        [ResponseDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @RequestDeviceId,
        @SealedRequest,
        NULL,
        NULL,
        @CreationDate,
        @ExpirationDate,
        NULL
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AgentFillApprovalRequest_ReadById]
    @Id     UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AgentFillApprovalRequest]
    WHERE
        [Id] = @Id
        AND [UserId] = @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AgentFillApprovalRequest_Answer]
    @Id               UNIQUEIDENTIFIER,
    @UserId           UNIQUEIDENTIFIER,
    @SealedResponse   VARCHAR(MAX),
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @ResponseDate     DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- First answer wins: the conditional update is atomic, so concurrent answers store exactly one response.
    UPDATE
        [dbo].[AgentFillApprovalRequest]
    SET
        [SealedResponse] = @SealedResponse,
        [ResponseDeviceId] = @ResponseDeviceId,
        [ResponseDate] = @ResponseDate
    WHERE
        [Id] = @Id
        AND [UserId] = @UserId
        AND [SealedResponse] IS NULL
        AND [ExpirationDate] > @ResponseDate

    SELECT @@ROWCOUNT
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AgentFillApprovalRequest_DeleteExpired]
    @ExpiredBefore DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[AgentFillApprovalRequest]
    WHERE
        [ExpirationDate] < @ExpiredBefore

    SELECT @@ROWCOUNT
END
GO
