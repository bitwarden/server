--Add OrganizationId Column and Foreign Key
IF COL_LENGTH('[dbo].[AuthRequest]', 'OrganizationId') IS NULL
BEGIN
ALTER TABLE
    [dbo].[AuthRequest]
    ADD [OrganizationId] UNIQUEIDENTIFIER NULL;
    ALTER TABLE 
    [dbo].[AuthRequest]
    ADD CONSTRAINT [FK_AuthRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
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

--Create SPROC with OrganizationId column
CREATE PROCEDURE [dbo].[AuthRequest_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER = NULL,
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
    [OrganizationId] = @OrganizationId,
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
    
--Create SPROC with OrganizationId column
CREATE PROCEDURE [dbo].[AuthRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER = NULL,
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
        [OrganizationId]
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
        @OrganizationId,
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
Go
    
-- New SPROC
CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_ReadAdminApprovalsByIds]
	@OrganizationId UNIQUEIDENTIFIER,
	@Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

SELECT
    ar.*, ou.[Email], ou.[Id] AS [OrganizationUserId]
FROM
    [dbo].[AuthRequestView] ar
    INNER JOIN
        [dbo].[OrganizationUser] ou ON ou.[UserId] = ar.[UserId] AND ou.[OrganizationId] = ar.[OrganizationId]
    WHERE
        ar.[OrganizationId] = @OrganizationId
    AND
        ar.[Type] = 2 -- AdminApproval
    AND
        ar.[Id] IN (SELECT [Id] FROM @Ids)
END
GO

-- New SPROC
CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_ReadPendingByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    ar.*, ou.[Email], ou.[OrganizationId], ou.[Id] AS [OrganizationUserId]
FROM
    [dbo].[AuthRequestView] ar
    INNER JOIN
        [dbo].[OrganizationUser] ou ON ou.[UserId] = ar.[UserId] AND ou.[OrganizationId] = ar.[OrganizationId]
    WHERE
        ar.[OrganizationId] = @OrganizationId
    AND
        ar.[ResponseDate] IS NULL
    AND
        ar.[Type] = 2 -- AdminApproval
END
GO