CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.UserId
    INTO
        #UserIds
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUIds ON OUIds.Id = OU.Id
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
END
GO
