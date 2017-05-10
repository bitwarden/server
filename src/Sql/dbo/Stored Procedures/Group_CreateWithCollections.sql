CREATE PROCEDURE [dbo].[Group_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
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
        0
    FROM
        @CollectionIds
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])
END