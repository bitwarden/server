CREATE PROCEDURE [dbo].[OrganizationUserUserDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        [Id] = @Id

    SELECT
        S.*
    FROM
        [dbo].[SubvaultView] S
    INNER JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = S.[Id]
    WHERE
        SU.[OrganizationUserId] = @Id
END