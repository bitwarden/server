CREATE PROCEDURE [dbo].[User_BumpManyAccountRevisionDates]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        [AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        @Ids IDs ON IDs.Id = U.Id
END
GO
