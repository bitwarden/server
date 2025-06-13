CREATE PROCEDURE [dbo].[Policy_ReadAcceptedOrConfirmedByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        P.*
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        [dbo].[OrganizationUser] OU ON P.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
    WHERE
        OU.[UserId] = @UserId
        AND (OU.[Status] = 1 OR OU.[Status] = 2) -- 1 = Accepted, 2 = Confirmed
END
