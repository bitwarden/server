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
    WHERE
        OU.[UserId] = @UserId
END
