CREATE PROCEDURE [dbo].[Subvault_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SubvaultView] S
    INNER JOIN
        [SubvaultUser] SU ON SU.[SubvaultId] = S.[Id]
    INNER JOIN
        [OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    INNER JOIN
        [Organization] O ON O.[Id] = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- Confirmed
        AND O.[Enabled] = 1
END