CREATE PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersById]
    @OrganizationUserIds AS NVARCHAR(MAX),
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    -- Declare a table variable to hold the parsed JSON data
    DECLARE @ParsedIds TABLE (Id UNIQUEIDENTIFIER);

    -- Parse the JSON input into the table variable
    INSERT INTO @ParsedIds (Id)
    SELECT value
    FROM OPENJSON(@OrganizationUserIds);

    -- Check if the input table is empty
    IF (SELECT COUNT(1) FROM @ParsedIds) < 1
    BEGIN
        RETURN(-1);
    END

    UPDATE
        [dbo].[OrganizationUser]
    SET [Status] = @Status
    WHERE [Id] IN (SELECT Id from @ParsedIds)

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END

