CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDate]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [User]
    SET
        [AccountRevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END