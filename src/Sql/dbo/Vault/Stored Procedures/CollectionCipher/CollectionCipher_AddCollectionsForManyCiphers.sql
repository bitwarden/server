CREATE PROCEDURE [dbo].[CollectionCipher_AddCollectionsForManyCiphers]
    @CipherIds AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #AvailableCollections (
        [Id] UNIQUEIDENTIFIER
    )

    INSERT INTO #AvailableCollections
        SELECT
            C.[Id]
        FROM
            [dbo].[Collection] C
        INNER JOIN
            [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
        WHERE
            O.[Id] = @OrganizationId AND O.[Enabled] = 1

    IF (SELECT COUNT(1) FROM #AvailableCollections) < 1
    BEGIN
        -- No collections available
        RETURN
    END

    ;WITH [SourceCollectionCipherCTE] AS(
        SELECT
            [Collection].[Id] AS [CollectionId],
            [Cipher].[Id] AS [CipherId]
        FROM
            @CollectionIds AS [Collection]
        CROSS JOIN
            @CipherIds AS [Cipher]
        WHERE
            [Collection].[Id] IN (SELECT [Id] FROM #AvailableCollections)
    )
    MERGE
        [CollectionCipher] AS [Target]
    USING
        [SourceCollectionCipherCTE] AS [Source]
    ON
        [Target].[CollectionId] = [Source].[CollectionId]
        AND [Target].[CipherId] = [Source].[CipherId]
    WHEN NOT MATCHED BY TARGET THEN
        INSERT VALUES
        (
            [Source].[CollectionId],
            [Source].[CipherId]
        )
   ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
