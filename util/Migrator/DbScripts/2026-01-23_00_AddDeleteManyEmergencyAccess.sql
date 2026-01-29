CREATE OR ALTER PROCEDURE [dbo].[EmergencyAccess_DeleteManyById]
    @EmergencyAccessIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    -- track GranteeIds for bumping revision date prior to deletion
    DECLARE @GranteeIds AS TABLE (UserId UNIQUEIDENTIFIER)

    -- this matches the logic in User_BumpAccountRevisionDateByEmergencyAccessGranteeId
    INSERT INTO @GranteeIds
        (UserId)
    SELECT DISTINCT GranteeId
    FROM
        [dbo].[EmergencyAccess] EA
    WHERE EA.Id IN (SELECT Id
    FROM @EmergencyAccessIds
    WHERE EA.[Status] = 2 )

    DECLARE @BatchSize INT = 100

    -- Delete EmergencyAccess Records
    WHILE @BatchSize > 0
    BEGIN

        DELETE TOP(@BatchSize) EA
        FROM
            [dbo].[EmergencyAccess] EA
            INNER JOIN
            @EmergencyAccessIds EAI ON EAI.Id = EA.Id

        SET @BatchSize = @@ROWCOUNT
    END

    -- Bump AccountRevisionDate for affected users after deletions
    Exec [dbo].[User_BumpManyAccountRevisionDates]
    (
        SELECT [UserId]
        FROM @GranteeIds
    )
END
GO