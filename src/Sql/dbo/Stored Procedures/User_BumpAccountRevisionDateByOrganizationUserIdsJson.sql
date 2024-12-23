CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson]
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
