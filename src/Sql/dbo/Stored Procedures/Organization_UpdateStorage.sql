CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER,
    @StorageIncrease BIGINT

AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = ISNULL([Storage], 0) + @StorageIncrease,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END