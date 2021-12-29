-- Create Auth Request table
IF OBJECT_ID('[dbo].[AuthRequest]') IS NULL
BEGIN
CREATE TABLE [dbo].[AuthRequest] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [Type]              SMALLINT         NOT NULL,
    [RequestDeviceId]   UNIQUEIDENTIFIER NOT NULL,
    [ResponseDeviceId]  UNIQUEIDENTIFIER NULL,
    [PublicKey]         VARCHAR(MAX)     NOT NULL,
    [Key]               VARCHAR(MAX)     NULL,
    [CreationDate]      DATETIME2 (7)    NOT NULL,
    [ResponseDate]      DATETIME2 (7)    NULL,
    CONSTRAINT [PK_AuthRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AuthRequest_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_AuthRequest_RequestDevice] FOREIGN KEY ([RequestDeviceId]) REFERENCES [dbo].[Device] ([Id]),
    CONSTRAINT [FK_AuthRequest_ResponseDevice] FOREIGN KEY ([ResponseDeviceId]) REFERENCES [dbo].[Device] ([Id])
);
END
GO


-- Create View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'AuthRequestView')
BEGIN
    DROP VIEW [dbo].[AuthRequestView];
END
GO

CREATE VIEW [dbo].[AuthRequestView]
AS
SELECT
    *
FROM
    [dbo].[AuthRequest]
GO


-- Auth Request CRUD sprocs
IF OBJECT_ID('[dbo].[AuthRequest_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[AuthRequest_Create]
END
GO

CREATE PROCEDURE [dbo].[AuthRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @RequestDeviceId UNIQUEIDENTIFIER,
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @PublicKey VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ResponseDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AuthRequest]
    (
        [Id],
        [UserId],
        [Type],
        [RequestDeviceId],
        [ResponseDeviceId],
        [PublicKey],
        [Key],
        [CreationDate],
        [ResponseDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Type,
        @RequestDeviceId,
        @ResponseDeviceId,
        @PublicKey,
        @Key,
        @CreationDate,
        @ResponseDate
    )
END
GO

IF OBJECT_ID('[dbo].[AuthRequest_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[AuthRequest_Update]
END
GO

CREATE PROCEDURE [dbo].[AuthRequest_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @RequestDeviceId UNIQUEIDENTIFIER,
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @PublicKey VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ResponseDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AuthRequest]
    SET
        [UserId] = @UserId,
        [Type] = @Type,
        [RequestDeviceId] = @RequestDeviceId,
        [ResponseDeviceId] = @ResponseDeviceId,
        [PublicKey] = @PublicKey,
        [Key] = @Key,
        [CreationDate] = @CreationDate,
        [ResponseDate] = @ResponseDate
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[AuthRequest_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[AuthRequest_ReadById]
END
GO

CREATE PROCEDURE [dbo].[AuthRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AuthRequestView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[AuthRequest_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[AuthRequest_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[AuthRequest_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE
        [Id] = @Id
END
GO
