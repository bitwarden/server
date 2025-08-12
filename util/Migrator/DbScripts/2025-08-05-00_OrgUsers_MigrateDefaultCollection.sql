CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_MigrateDefaultCollection]
    @Ids [dbo].[GuidIdArray] READONLY,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE c
    SET
        [DefaultUserCollectionEmail] = CASE WHEN c.[DefaultUserCollectionEmail] IS NULL THEN u.[Email] ELSE c.[DefaultUserCollectionEmail] END,
        [RevisionDate] = @RevisionDate,
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
