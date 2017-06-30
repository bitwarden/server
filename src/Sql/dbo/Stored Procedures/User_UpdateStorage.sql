CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER,
    @StorageIncrease BIGINT

AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Storage] = ISNULL([Storage], 0) + @StorageIncrease,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END