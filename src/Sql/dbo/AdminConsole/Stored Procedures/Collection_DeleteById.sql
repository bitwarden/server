CREATE PROCEDURE [dbo].[Collection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Collection] WHERE [Id] = @Id)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
    END

    DELETE
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @Id

    DELETE
    FROM
        [dbo].[Collection]
    WHERE
        [Id] = @Id
END