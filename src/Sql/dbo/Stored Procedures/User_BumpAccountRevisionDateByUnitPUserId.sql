CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByUnitPUserId]
    @UnitPUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[UnitPUser] OU ON OU.[UserId] = U.[Id]
    WHERE
        OU.[Id] = @UnitPUserId
        AND OU.[Status] = 2 -- Confirmed
END
