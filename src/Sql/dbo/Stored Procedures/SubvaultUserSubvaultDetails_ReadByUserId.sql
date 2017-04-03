CREATE PROCEDURE [dbo].[SubvaultUserSubvaultDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SU.*
    FROM
        [dbo].[SubvaultUserSubvaultDetailsView] SU
    INNER JOIN
        [OrganizationUser] OU ON SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[UserId] = @UserId
END