CREATE PROCEDURE [dbo].[Cipher_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CipherAttachments NVARCHAR(MAX)
    SELECT
        @CipherAttachments = [Attachments] 
    FROM 
        [dbo].[Cipher]
    WHERE [Id] = @Id

    DECLARE @Size BIGINT

    SELECT 
        @Size = SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
    FROM
        OPENJSON(@CipherAttachments)

    DECLARE @SizeDec BIGINT = @Size * -1

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = NULL,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [Attachments] = @Attachments,
        [RevisionDate] = @RevisionDate
        -- No need to update CreationDate, Favorites, Folders, or Type since that data will not change
    WHERE
        [Id] = @Id

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            S.[Id]
        FROM
            [dbo].[Collection] S
        INNER JOIN
            [Organization] O ON O.[Id] = S.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId]
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
    )
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
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])

    EXEC [dbo].[Organization_UpdateStorage] @OrganizationId, @Size
    EXEC [dbo].[User_UpdateStorage] @UserId, @SizeDec
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END