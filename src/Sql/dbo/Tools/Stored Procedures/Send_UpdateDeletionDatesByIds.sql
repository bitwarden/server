CREATE PROCEDURE [dbo].[Send_UpdateDeletionDatesByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @DeletionHours INT
AS
BEGIN
    SET NOCOUNT ON

    -- Set field
    UPDATE
        [dbo].[Send]
    SET
        [DeletionDate] = DATEADD(HOUR, @DeletionHours, [CreationDate]),
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] IN (SELECT * FROM @Ids)

    -- Bump account revision dates
    EXEC [dbo].[User_BumpManyAccountRevisionDates]
    (
        SELECT DISTINCT
            UserId
        FROM
            [dbo].[Send]
        WHERE
            [Id] IN (SELECT * FROM @Ids)
    )
END