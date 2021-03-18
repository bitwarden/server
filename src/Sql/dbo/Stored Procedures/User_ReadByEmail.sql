CREATE PROCEDURE [dbo].[User_ReadByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Email] = @Email
END