CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsAdmin]
    @CipherId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -- Available collections for this org, excluding default collections
    SELECT
        C.[Id]
    INTO #TempAvailableCollections
    FROM [dbo].[Collection] AS C
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[Type] <> 1;  -- exclude DefaultUserCollection

    -- Insert new collection assignments
    INSERT INTO [dbo].[CollectionCipher] (
        [CollectionId],
        [CipherId]
    )
    SELECT
        S.[Id],
        @CipherId
    FROM @CollectionIds AS S
    INNER JOIN #TempAvailableCollections AS A
        ON A.[Id] = S.[Id]
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[CollectionCipher] AS CC
        WHERE CC.[CollectionId] = S.[Id]
          AND CC.[CipherId]    = @CipherId
    );

    -- Delete removed collection assignments
    DELETE CC
    FROM [dbo].[CollectionCipher] AS CC
    INNER JOIN #TempAvailableCollections AS A
        ON A.[Id] = CC.[CollectionId]
    WHERE CC.[CipherId] = @CipherId
      AND NOT EXISTS (
          SELECT 1
          FROM @CollectionIds AS S
          WHERE S.[Id] = CC.[CollectionId]
      );

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId;
    END

    DROP TABLE #TempAvailableCollections;
END
GO
