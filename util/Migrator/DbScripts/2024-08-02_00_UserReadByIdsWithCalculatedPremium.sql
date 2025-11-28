CREATE OR ALTER PROCEDURE [dbo].[User_ReadByIdsWithCalculatedPremium]
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
        CASE
            WHEN U.[Premium] = 1
                OR EXISTS (
                    SELECT 1
                    FROM [dbo].[OrganizationUser] OU
                    JOIN [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
                    WHERE OU.[UserId] = U.[Id]
                        AND O.[UsersGetPremium] = 1
                        AND O.[Enabled] = 1
                )
                THEN 1
            ELSE 0
            END AS HasPremiumAccess
    FROM
        [dbo].[UserView] U
    WHERE
        U.[Id] IN (SELECT [Id] FROM @ParsedIds);
END;
