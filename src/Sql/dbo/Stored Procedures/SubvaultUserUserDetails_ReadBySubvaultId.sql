CREATE PROCEDURE [dbo].[SubvaultUserUserDetails_ReadBySubvaultId]
    @SubvaultId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SU.*
    FROM
        [dbo].[SubvaultUserUserDetailsView] SU
    INNER JOIN
        [OrganizationUser] OU ON SU.[OrganizationUserId] = OU.[Id]
    WHERE
        SU.[SubvaultId] = @SubvaultId
END