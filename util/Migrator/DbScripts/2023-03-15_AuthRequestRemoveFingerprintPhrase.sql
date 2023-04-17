-- Description: Remove RequestFingerprint column from AuthRequest table and recreate view and stored procedures without it.
IF COL_LENGTH('[dbo].[AuthRequest]', 'RequestFingerprint') IS NOT NULL
    BEGIN
        ALTER TABLE
            [dbo].[AuthRequest]
        DROP COLUMN
            [RequestFingerprint]
    END
GO

-- Drop and recreate view
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'AuthRequestView')
    BEGIN
        DROP VIEW [dbo].[AuthRequestView]
    END
GO

CREATE VIEW [dbo].[AuthRequestView]
AS
SELECT
    *
FROM
    [dbo].[AuthRequest]
GO

--Drop existing SPROC
IF OBJECT_ID('[dbo].[AuthRequest_Update]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[AuthRequest_Update]
    END
GO

--Create SPROC without RequestFingerprint column
CREATE PROCEDURE [dbo].[AuthRequest_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Type SMALLINT, 
    @RequestDeviceIdentifier NVARCHAR(50),
    @RequestDeviceType SMALLINT,
    @RequestIpAddress VARCHAR(50),
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @AccessCode VARCHAR(25),
    @PublicKey VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @MasterPasswordHash VARCHAR(MAX),
    @Approved BIT,
    @CreationDate DATETIME2 (7),
    @ResponseDate DATETIME2 (7),
    @AuthenticationDate DATETIME2 (7)    
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[AuthRequest]
SET
    [UserId] = @UserId,
    [Type] = @Type,
    [RequestDeviceIdentifier] = @RequestDeviceIdentifier,
    [RequestDeviceType] = @RequestDeviceType,
    [RequestIpAddress] = @RequestIpAddress,
    [ResponseDeviceId] = @ResponseDeviceId,
    [AccessCode] = @AccessCode,
    [PublicKey] = @PublicKey,
    [Key] = @Key,
    [MasterPasswordHash] = @MasterPasswordHash,
    [Approved] = @Approved,
    [CreationDate] = @CreationDate,
    [ResponseDate] = @ResponseDate,
    [AuthenticationDate] = @AuthenticationDate
WHERE
    [Id] = @Id
END
GO

--Drop existing SPROC
IF OBJECT_ID('[dbo].[AuthRequest_Create]') IS NOT NULL
BEGIN
        DROP PROCEDURE [dbo].[AuthRequest_Create]
END
GO

--Create SPROC without RequestFingerprint column
CREATE PROCEDURE [dbo].[AuthRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @RequestDeviceIdentifier NVARCHAR(50),
    @RequestDeviceType TINYINT,
    @RequestIpAddress VARCHAR(50),
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @AccessCode VARCHAR(25),
    @PublicKey VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @MasterPasswordHash VARCHAR(MAX),
    @Approved BIT,
    @CreationDate DATETIME2(7),
    @ResponseDate DATETIME2(7),
    @AuthenticationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AuthRequest]
    (
        [Id],
        [UserId],
        [Type],
        [RequestDeviceIdentifier],
        [RequestDeviceType],
        [RequestIpAddress],
        [ResponseDeviceId],
        [AccessCode],
        [PublicKey],
        [Key],
        [MasterPasswordHash],
        [Approved],
        [CreationDate],
        [ResponseDate],
        [AuthenticationDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Type,
        @RequestDeviceIdentifier,
        @RequestDeviceType,
        @RequestIpAddress,
        @ResponseDeviceId,
        @AccessCode,
        @PublicKey,
        @Key,
        @MasterPasswordHash,
        @Approved,
        @CreationDate,
        @ResponseDate,
        @AuthenticationDate
    )
END