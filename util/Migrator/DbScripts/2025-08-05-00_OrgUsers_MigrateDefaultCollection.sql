CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_MigrateDefaultCollection]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE c
    SET
        [DefaultUserCollectionEmail] = u.[Email],
        [Type] = 0
    FROM
        [dbo].[Collection] c
        INNER JOIN [dbo].[CollectionUser] cu ON c.[Id] = cu.[CollectionId]
        INNER JOIN [dbo].[OrganizationUser] ou ON cu.[OrganizationUserId] = ou.[Id]
        INNER JOIN [dbo].[User] u ON ou.[UserId] = u.[Id]
        INNER JOIN @Ids i ON ou.[Id] = i.[Id]
    WHERE
        c.[Type] = 1
END
GO
