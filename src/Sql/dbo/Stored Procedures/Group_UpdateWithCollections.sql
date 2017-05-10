CREATE PROCEDURE [dbo].[Group_UpdateWithCollections]
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

    EXEC [dbo].[Group_Update] @Id, @OrganizationId, @Name, @AccessAll, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING 
        @CollectionIds AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[GroupId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @Id,
            0
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @Id THEN
        DELETE
    ;

    -- TODO: Update user revision date times that this affects
END