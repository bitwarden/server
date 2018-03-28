IF OBJECT_ID('[dbo].[User_Search]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Search]
END
GO

CREATE PROCEDURE [dbo].[User_Search]
    @Email NVARCHAR(50),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @EmailLikeSearch NVARCHAR(55) = @Email + '%'

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        (@Email IS NULL OR [Email] LIKE @EmailLikeSearch)
    ORDER BY [Email] ASC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
GO

IF OBJECT_ID('[dbo].[Organization_Search]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Search]
END
GO

CREATE PROCEDURE [dbo].[Organization_Search]
    @Name NVARCHAR(50),
    @UserEmail NVARCHAR(50),
    @Paid BIT,
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @NameLikeSearch NVARCHAR(55) = '%' + @Name + '%'

    IF @UserEmail IS NOT NULL
    BEGIN
        SELECT
            O.*
        FROM
            [dbo].[OrganizationView] O
        INNER JOIN
            [dbo].[OrganizationUser] OU ON O.[Id] = OU.[OrganizationId]
        INNER JOIN
            [dbo].[User] U ON U.[Id] = OU.[UserId]
        WHERE
            (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
            AND (@UserEmail IS NULL OR U.[Email] = @UserEmail)
            AND
            (
                @Paid IS NULL OR
                (
                    (@Paid = 1 AND O.[GatewaySubscriptionId] IS NOT NULL) OR
                    (@Paid = 0 AND O.[GatewaySubscriptionId] IS NULL)
                )
            )
        ORDER BY O.[CreationDate] DESC
        OFFSET @Skip ROWS
        FETCH NEXT @Take ROWS ONLY
    END
    ELSE
    BEGIN
        SELECT
            O.*
        FROM
            [dbo].[OrganizationView] O
        WHERE
            (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
            AND
            (
                @Paid IS NULL OR
                (
                    (@Paid = 1 AND O.[GatewaySubscriptionId] IS NOT NULL) OR
                    (@Paid = 0 AND O.[GatewaySubscriptionId] IS NULL)
                )
            )
        ORDER BY O.[CreationDate] DESC
        OFFSET @Skip ROWS
        FETCH NEXT @Take ROWS ONLY
    END
END
GO

IF OBJECT_ID('[dbo].[Folder_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Folder_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Folder_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM [dbo].[Folder] WHERE [Id] = @Id)
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$."', @UserId, '"')

    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
    WHERE
        (
            C.[UserId] = @UserId
            OR (
                C.[UserId] IS NULL
                AND (OU.[AccessAll] = 1 OR CU.[CollectionId] IS NOT NULL)
            )
        )
        AND JSON_VALUE(C.[Folders], @UserIdPath) = @Id

    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
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
            [dbo].[Cipher]
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [CTE] ON C.[Id] = [CTE].[Id]
    WHERE
        C.[OrganizationId] = @Id

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
            [dbo].[Cipher]
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [CTE] ON C.[Id] = [CTE].[Id]
    WHERE
        C.[UserId] = @Id

    UPDATE
        [dbo].[User]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
