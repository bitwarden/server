
CREATE PROCEDURE [dbo].[User_ReadAccountRevisionDateById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [AccountRevisionDate]
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id
END
