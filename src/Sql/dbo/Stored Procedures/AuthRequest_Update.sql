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