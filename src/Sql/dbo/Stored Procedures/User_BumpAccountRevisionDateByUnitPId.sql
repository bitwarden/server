CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByUnitPId]
    @UnitPId UNIQUEIDENTIFIER
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
        OU.[UnitPId] = @UnitPId
        AND OU.[Status] = 2 -- Confirmed
END
