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
    WHERE
        SU.[UserId] = @UserId
END