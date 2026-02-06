ALTER TABLE
  [dbo].[AuthRequest]
ADD
  [RequestCountryName] NVARCHAR(200) NULL;
GO

EXECUTE sp_refreshview 'dbo.AuthRequestView'
GO

CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER = NULL,
    @Type TINYINT,
    @RequestDeviceIdentifier NVARCHAR(50),
    @RequestDeviceType TINYINT,
    @RequestIpAddress VARCHAR(50),
    @RequestCountryName NVARCHAR(200),
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
        [OrganizationId],
        [Type],
        [RequestDeviceIdentifier],
        [RequestDeviceType],
        [RequestIpAddress],
        [RequestCountryName],
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
            @RequestCountryName,
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER = NULL,
    @Type SMALLINT,
    @RequestDeviceIdentifier NVARCHAR(50),
    @RequestDeviceType SMALLINT,
    @RequestIpAddress VARCHAR(50),
    @RequestCountryName NVARCHAR(200),
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
    [RequestCountryName] = @RequestCountryName,
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

CREATE OR ALTER PROCEDURE AuthRequest_UpdateMany
    @jsonData NVARCHAR(MAX)
AS
BEGIN
    UPDATE AR
    SET
        [Id] = ARI.[Id],
        [UserId] = ARI.[UserId],
        [Type] = ARI.[Type],
        [RequestDeviceIdentifier] = ARI.[RequestDeviceIdentifier],
        [RequestDeviceType] = ARI.[RequestDeviceType],
        [RequestIpAddress] = ARI.[RequestIpAddress],
        [RequestCountryName] = ARI.[RequestCountryName],
        [ResponseDeviceId] = ARI.[ResponseDeviceId],
        [AccessCode] = ARI.[AccessCode],
        [PublicKey] = ARI.[PublicKey],
        [Key] = ARI.[Key],
        [MasterPasswordHash] = ARI.[MasterPasswordHash],
        [Approved] = ARI.[Approved],
        [CreationDate] = ARI.[CreationDate],
        [ResponseDate] = ARI.[ResponseDate],
        [AuthenticationDate] = ARI.[AuthenticationDate],
        [OrganizationId] = ARI.[OrganizationId]
    FROM
        [dbo].[AuthRequest] AR
        INNER JOIN
        OPENJSON(@jsonData)
        WITH (
            Id UNIQUEIDENTIFIER '$.Id',
            UserId UNIQUEIDENTIFIER '$.UserId',
            Type SMALLINT '$.Type',
            RequestDeviceIdentifier NVARCHAR(50) '$.RequestDeviceIdentifier',
            RequestDeviceType SMALLINT '$.RequestDeviceType',
            RequestIpAddress VARCHAR(50) '$.RequestIpAddress',
            RequestCountryName NVARCHAR(200) '$.RequestCountryName',
            ResponseDeviceId UNIQUEIDENTIFIER '$.ResponseDeviceId',
            AccessCode VARCHAR(25) '$.AccessCode',
            PublicKey VARCHAR(MAX) '$.PublicKey',
            [Key] VARCHAR(MAX) '$.Key',
            MasterPasswordHash VARCHAR(MAX) '$.MasterPasswordHash',
            Approved BIT '$.Approved',
            CreationDate DATETIME2 '$.CreationDate',
            ResponseDate DATETIME2 '$.ResponseDate',
            AuthenticationDate DATETIME2 '$.AuthenticationDate',
            OrganizationId UNIQUEIDENTIFIER '$.OrganizationId'
        ) ARI ON AR.Id = ARI.Id;
END
GO