CREATE PROCEDURE [dbo].[EmergencyAccess_DeleteManyById]
    @EmergencyAccessIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIds AS [GuidIdArray];

    INSERT INTO @UserIds
    SELECT DISTINCT
        [GranteeId]
    FROM
        [dbo].[EmergencyAccess] EA
    INNER JOIN
        @EmergencyAccessIds EAI ON EAI.[Id] = EA.[Id]
    WHERE
        EA.[Status] = 2
    AND
        EA.[GranteeId] IS NOT NULL

    DECLARE @BatchSize INT = 100

    -- Delete EmergencyAccess Records
    WHILE @BatchSize > 0
    BEGIN

        DELETE TOP(@BatchSize) EA
        FROM
            [dbo].[EmergencyAccess] EA
        INNER JOIN
            @EmergencyAccessIds EAI ON EAI.[Id] = EA.[Id]

        SET @BatchSize = @@ROWCOUNT

    END

    -- Bump AccountRevisionDate for affected users after deletions
    Exec [dbo].[User_BumpManyAccountRevisionDates] @UserIds

END
GO
