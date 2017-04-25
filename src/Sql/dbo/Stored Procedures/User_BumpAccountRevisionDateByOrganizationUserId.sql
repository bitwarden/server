CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserId]
    @OrganizationUserId UNIQUEIDENTIFIER
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
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    WHERE
        OU.[Id] = @OrganizationUserId
        AND OU.[Status] = 2 -- Confirmed
END