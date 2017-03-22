CREATE PROCEDURE [dbo].[CipherDetails_ReadByRevisionDateUserWithDeleteHistory]
    @SinceRevisionDate DATETIME2(7),
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherDetails](@UserId) C
    WHERE
        [RevisionDate] > @SinceRevisionDate
        AND [UserId] = @UserId

    SELECT
        [CipherId]
    FROM
        [dbo].[History]
    WHERE
        [Date] > @SinceRevisionDate
        AND [Event] = 2 -- Only cipher delete events.
        AND [UserId] = @UserId
END
