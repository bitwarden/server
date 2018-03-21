IF OBJECT_ID('[dbo].[User_SearchByEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_SearchByEmail]
END
GO

CREATE PROCEDURE [dbo].[User_SearchByEmail]
    @Email NVARCHAR(50),
    @Skip INT = 0,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @EmailLikeSearch NVARCHAR(55) = @Email + '%'

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
GO
