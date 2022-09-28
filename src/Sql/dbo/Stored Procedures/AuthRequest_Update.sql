CREATE PROCEDURE [dbo].[AuthRequest_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @Key VARCHAR(MAX),
    @MasterPasswordHash VARCHAR(MAX),
    @ResponseDate DATETIME2(7),
    @AuthenticationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AuthRequest]
    SET
        [ResponseDeviceId] = @ResponseDeviceId,
        [Key] = @Key,
        [MasterPasswordHash] = @MasterPasswordHash,
        [ResponseDate] = @ResponseDate,
        [AuthenticationDate] = @AuthenticationDate
    WHERE
        [Id] = @Id
END
