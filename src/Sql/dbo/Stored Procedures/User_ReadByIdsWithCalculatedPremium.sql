CREATE PROCEDURE [dbo].[User_ReadByIdsWithCalculatedPremium]
    @Ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    -- Declare a table variable to hold the parsed JSON data
    DECLARE @ParsedIds TABLE (Id UNIQUEIDENTIFIER);

    -- Parse the JSON input into the table variable
    INSERT INTO @ParsedIds (Id)
    SELECT value
    FROM OPENJSON(@Ids);

    -- Check if the input table is empty
    IF (SELECT COUNT(1) FROM @ParsedIds) < 1
    BEGIN
        RETURN(-1);
    END

    -- Main query to fetch user details and calculate premium access
    SELECT
        U.*,
        ISNULL(UPA.[HasPremiumAccess], 0) AS HasPremiumAccess
    FROM
        [dbo].[UserView] U
    LEFT JOIN
        [dbo].[UserPremiumAccessView] UPA ON UPA.[UserId] = U.[Id]
    WHERE
        U.[Id] IN (SELECT [Id] FROM @ParsedIds);
END;
