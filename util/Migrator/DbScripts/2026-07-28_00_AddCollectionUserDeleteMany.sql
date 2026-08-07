CREATE OR ALTER PROCEDURE [dbo].[CollectionUser_DeleteMany]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [CollectionId] IN (SELECT [Id] FROM @CollectionIds)
        AND [OrganizationUserId] IN (SELECT [Id] FROM @OrganizationUserIds)

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END
