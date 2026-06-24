CREATE PROCEDURE [dbo].[Send_UpdateDeletionDatesByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @DeletionHours INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIds [dbo].[GuidIdArray]

    -- Set field
    UPDATE
        [dbo].[Send]
    SET
        [DeletionDate] = DATEADD(HOUR, @DeletionHours, [CreationDate]),
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] IN (SELECT * FROM @Ids)

    -- Bump account revision dates
    INSERT INTO @UserIds
    SELECT DISTINCT
        UserId
    FROM
        [dbo].[Send]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
        AND [UserId] IS NOT NULL

    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UserIds
END