CREATE PROCEDURE [dbo].[Collection_UpdateWithGroupsAndUsers]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    -- Groups
    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Group]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING
        @Groups AS [Source]
    ON
        [Target].[CollectionId] = @Id
        AND [Target].[GroupId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT -- Add explicit column list
        (
	        [CollectionId],
	        [GroupId],
	        [ReadOnly],
	        [HidePasswords],
            [Manage]
    	)
        VALUES
        (
            @Id,
            [Source].[Id],
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
    AND [Target].[CollectionId] = @Id THEN
        DELETE
    ;

    -- Users
    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[OrganizationUser]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionUser] AS [Target]
    USING
        @Users AS [Source]
    ON
        [Target].[CollectionId] = @Id
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT
        (
	        [CollectionId],
	        [OrganizationUserId],
	        [ReadOnly],
	        [HidePasswords],
            [Manage]
    	)
        VALUES
        (
            @Id,
            [Source].[Id],
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
    AND [Target].[CollectionId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
