CREATE PROCEDURE [dbo].[AuthRequest_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
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
