CREATE PROCEDURE [dbo].[User_Search]
    @Email NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @EmailLikeSearch NVARCHAR(261) = @Email + '%'

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        (@Email IS NULL OR [Email] LIKE @EmailLikeSearch)
    ORDER BY [Email] ASC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END