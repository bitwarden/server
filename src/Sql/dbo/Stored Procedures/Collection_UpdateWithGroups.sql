CREATE PROCEDURE [dbo].[Collection_UpdateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type

    -- Bump RevisionDate on all affected groups (old + new) before modifying CollectionGroup
    ;WITH [AffectedGroupsCTE] AS (
        SELECT
            g.[Id]
        FROM
            @Groups g

        UNION

        SELECT
            CG.[GroupId]
        FROM
            [dbo].[CollectionGroup] CG
        WHERE
            CG.[CollectionId] = @Id
    )
    UPDATE
        G
    SET
        G.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Group] G
    WHERE
        G.[OrganizationId] = @OrganizationId
        AND G.[Id] IN (SELECT [Id] FROM [AffectedGroupsCTE])

    -- Groups
    -- Delete groups that are no longer in source
    DELETE
        cg
    FROM
        [dbo].[CollectionGroup] cg
    LEFT JOIN
        @Groups g ON cg.GroupId = g.Id
    WHERE
        cg.CollectionId = @Id
        AND g.Id IS NULL;

    -- Update existing groups
    UPDATE
        cg
    SET
        cg.ReadOnly = g.ReadOnly,
        cg.HidePasswords = g.HidePasswords,
        cg.Manage = g.Manage
    FROM
        [dbo].[CollectionGroup] cg
    INNER JOIN
        @Groups g ON cg.GroupId = g.Id
    WHERE
        cg.CollectionId = @Id
        AND (
            cg.ReadOnly != g.ReadOnly
            OR cg.HidePasswords != g.HidePasswords
            OR cg.Manage != g.Manage
        );

    -- Insert new groups
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        @Id,
        g.Id,
        g.ReadOnly,
        g.HidePasswords,
        g.Manage
    FROM
        @Groups g
    INNER JOIN
        [dbo].[Group] grp ON grp.Id = g.Id
    LEFT JOIN
        [dbo].[CollectionGroup] cg ON cg.CollectionId = @Id AND cg.GroupId = g.Id
    WHERE
        grp.OrganizationId = @OrganizationId
        AND cg.CollectionId IS NULL;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
