CREATE PROCEDURE [dbo].[Group_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Create] @Id, @OrganizationId, @Name, @AccessAll, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly]
    )
    SELECT
        [Id],
        @Id,
        [ReadOnly]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])
END