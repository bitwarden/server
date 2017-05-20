CREATE PROCEDURE [dbo].[Collection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

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