﻿CREATE PROCEDURE [dbo].[AuthRequest_Create]
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
