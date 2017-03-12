CREATE PROCEDURE [dbo].[SubvaultUserDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SU.*
    FROM
        [dbo].[SubvaultUserDetailsView] SU
    INNER JOIN
        [OrganizationUser] OU ON SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[UserId] = @UserId
END