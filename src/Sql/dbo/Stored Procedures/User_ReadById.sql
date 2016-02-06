CREATE PROCEDURE [dbo].[User_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Id] = @Id
END
