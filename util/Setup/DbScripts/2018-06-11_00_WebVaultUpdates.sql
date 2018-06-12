IF OBJECT_ID('[dbo].[Collection_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE
            WHEN
                OU.[AccessAll] = 1
                OR G.[AccessAll] = 1
                OR CU.[ReadOnly] = 0
                OR CG.[ReadOnly] = 0
            THEN 1
            ELSE 0
        END [ReadOnly]
    FROM
        [dbo].[CollectionView] C
    INNER JOIN
        [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO

IF OBJECT_ID('[dbo].[Organization_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @Id

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            #Temp
    )
    SELECT
        @Storage = SUM([Size])
    FROM
        [CTE]

    DROP TABLE #Temp

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[User_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] = @Id

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            #Temp
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [CTE]

    DROP TABLE #Temp

    UPDATE
        [dbo].[User]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
