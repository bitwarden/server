CREATE PROCEDURE [dbo].[OrganizationUser_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [OrganizationUser_ReadById] @Id

    SELECT
        CU.[CollectionId] Id,
        CU.[ReadOnly],
        CU.[HidePasswords],
        CU.[Manage]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = [OU].[Id]
    WHERE
        [OrganizationUserId] = @Id
END
