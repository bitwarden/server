CREATE PROCEDURE [dbo].[Group_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(100),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[CollectionAccessSelectionType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    -- Bump RevisionDate on all affected collections
    ;WITH [AffectedCollectionsCTE] AS (
        SELECT [Id] FROM @Collections
        UNION
        SELECT CG.[CollectionId]
        FROM [dbo].[CollectionGroup] CG
        WHERE CG.[GroupId] = @Id
    )
    UPDATE C
    SET C.[RevisionDate] = @RevisionDate
    FROM [dbo].[Collection] C
    WHERE C.[OrganizationId] = @OrganizationId
    AND C.[Id] IN (SELECT [Id] FROM [AffectedCollectionsCTE])

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
        @Collections AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[GroupId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT
        (
        	[CollectionId],
        	[GroupId],
        	[ReadOnly],
        	[HidePasswords],
            [Manage]
    	)
        VALUES
        (
            [Source].[Id],
            @Id,
            [Source].[ReadOnly],
            [Source].[HidePasswords],
            [Source].[Manage]
        )
    WHEN MATCHED AND (
        [Target].[ReadOnly] != [Source].[ReadOnly]
        OR [Target].[HidePasswords] != [Source].[HidePasswords]
        OR [Target].[Manage] != [Source].[Manage]
    ) THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly],
                   [Target].[HidePasswords] = [Source].[HidePasswords],
                   [Target].[Manage] = [Source].[Manage]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
