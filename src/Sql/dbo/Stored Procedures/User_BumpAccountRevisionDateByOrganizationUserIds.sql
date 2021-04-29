CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        @OrganizationUserIds OUIDs
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OUIDs.Id = OU.Id AND OU.[Status] = 2 -- Confirmed
    INNER JOIN
        [dbo].[User] U ON OU.UserId = U.Id
END
GO
