CREATE OR ALTER PROCEDURE [dbo].[User_SetUserKeyId]
    @Id UNIQUEIDENTIFIER,
    @UserKeyId VARCHAR(32),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [UserKeyId] = @UserKeyId,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
