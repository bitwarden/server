CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        UserId
    INTO
        #UserIds
    FROM
        [dbo].[OrganizationUser] OU
        INNER JOIN
        @OrganizationUserIds OUIds on OUIds.Id = OU.Id
    WHERE
        OU.[Status] = 2
    -- Confirmed

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
        Inner JOIN
        #UserIds ON U.[Id] = #UserIds.[UserId]
END
GO
