CREATE PROCEDURE [dbo].[AuthRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @RequestDeviceIdentifier NVARCHAR(50),
    @RequestDeviceType TINYINT,
    @RequestIpAddress VARCHAR(50),
    @RequestFingerprint VARCHAR(MAX),
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
        [RequestFingerprint],
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
        @RequestFingerprint,
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
