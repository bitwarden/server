CREATE PROCEDURE [dbo].[Policy_ReadByUserId]
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
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
END