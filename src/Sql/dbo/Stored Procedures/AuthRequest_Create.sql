CREATE PROCEDURE [dbo].[AuthRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @RequestDeviceId UNIQUEIDENTIFIER,
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @PublicKey VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @MasterPasswordHash VARCHAR(MAX),
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
        [MasterPasswordHash],
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
        @MasterPasswordHash,
        @CreationDate,
        @ResponseDate
    )
END
