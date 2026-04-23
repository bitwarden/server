CREATE PROCEDURE [dbo].[CollectionUser_Delete]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [CollectionId] = @CollectionId
        AND [OrganizationUserId] = @OrganizationUserId

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END