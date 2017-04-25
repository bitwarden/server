CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
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
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- Confirmed
END