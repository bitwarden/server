CREATE PROCEDURE [dbo].[Cipher_UpdateCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF @OrganizationId IS NULL OR (SELECT COUNT(1) FROM @CollectionIds) < 1
    BEGIN
        RETURN(-1)
    END

    CREATE TABLE #AvailableCollections (
        [Id] UNIQUEIDENTIFIER
    )

    IF @UserId IS NULL
    BEGIN
        INSERT INTO #AvailableCollections
            SELECT
                [Id]
            FROM
                [dbo].[Collection]
            WHERE
                [OrganizationId] = @OrganizationId
    END
    ELSE
    BEGIN
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
    END

    IF (SELECT COUNT(1) FROM #AvailableCollections) < 1
    BEGIN
        -- No writable collections available to share with in this organization.
        RETURN(-1)
    END

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    SELECT
        [Id],
        @Id
    FROM
        @CollectionIds
    WHERE
        [Id] IN (SELECT [Id] FROM #AvailableCollections)

    RETURN(0)
END