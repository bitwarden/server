CREATE PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    CREATE TABLE #TempAvailableCollections
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableCollections
    SELECT
        Id
    FROM
        [dbo].[Collection]
    WHERE
        OrganizationId = @OrganizationId

    -- Update
    UPDATE
        [Target]
    SET
        [Target].[ReadOnly] = [Source].[ReadOnly]
    FROM
        [dbo].[CollectionUser] AS [Target]
    INNER JOIN
        @Collections AS [Source] ON [Source].[Id] = [Target].[CollectionId]
    WHERE
        [Target].[OrganizationUserId] = @Id
        AND [Target].[ReadOnly] != [Source].[ReadOnly]

    -- Insert
    INSERT INTO
        [dbo].[CollectionUser]
    SELECT
        [Source].[Id],
        @Id,
        [Source].[ReadOnly]
    FROM
        @Collections AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableCollections)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[CollectionUser]
            WHERE
                [CollectionId] = [Source].[Id]
                AND [OrganizationUserId] = @Id
        )
    
    -- Delete
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    WHERE
        CU.[OrganizationUserId] = @Id
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @Collections
            WHERE
                [Id] = CU.[CollectionId]
        )

    DROP TABLE #TempAvailableCollections
END