CREATE OR ALTER PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson]
    @OrganizationUserIds NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #UserIds
    (
        UserId UNIQUEIDENTIFIER NOT NULL
    );

    INSERT INTO #UserIds (UserId)
    SELECT
        OU.UserId
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        (SELECT [value] as Id FROM OPENJSON(@OrganizationUserIds)) AS OUIds
        ON OUIds.Id = OU.Id
    WHERE
        OU.[Status] = 2 -- Confirmed

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        #UserIds ON U.[Id] = #UserIds.[UserId]

    DROP TABLE #UserIds
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersById]
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson] @OrganizationUserIds
END
GO
