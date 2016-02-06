CREATE PROCEDURE [dbo].[User_ReadByEmail]
    @Email NVARCHAR(50)
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Email] = @Email
END
