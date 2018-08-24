CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsForCiphers]
    @CipherIds AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
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
            [Organization] O ON O.[Id] = C.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
        WHERE
            O.[Id] = @OrganizationId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (
                OU.[AccessAll] = 1
                OR CU.[ReadOnly] = 0
                OR G.[AccessAll] = 1
                OR CG.[ReadOnly] = 0
            )

    IF (SELECT COUNT(1) FROM #AvailableCollections) < 1
    BEGIN
        -- No writable collections available to share with in this organization.
        RETURN
    END

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    SELECT 
        [Collection].[Id],
        [Cipher].[Id]
    FROM
        @CollectionIds [Collection]
    INNER JOIN
        @CipherIds [Cipher] ON 1 = 1
    WHERE
        [Collection].[Id] IN (SELECT [Id] FROM #AvailableCollections)

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
