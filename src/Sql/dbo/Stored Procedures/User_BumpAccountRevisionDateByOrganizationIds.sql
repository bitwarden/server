CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationIds]
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
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
        OU.[OrganizationId] IN (SELECT [Id] FROM @OrganizationIds)
        AND OU.[Status] = 2 -- Confirmed
END