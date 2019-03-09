PRINT N'Updating [dbo].[Cipher]...';

GO
ALTER TABLE [dbo].[Cipher] ADD
  [PwnedCheckDate] DATETIME2 (7)    NULL,
  [Pwned]          BIT              NULL;


GO
PRINT N'Updating [dbo].[CipherDetails_Create]...';

IF OBJECT_ID('[dbo].[CipherDetails_Create]') IS NOT NULL
  BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_Create]
  END
GO

GO
CREATE PROCEDURE [dbo].[CipherDetails_Create]
  @Id UNIQUEIDENTIFIER,
  @UserId UNIQUEIDENTIFIER,
  @OrganizationId UNIQUEIDENTIFIER,
  @Type TINYINT,
  @Data NVARCHAR(MAX),
  @Favorites NVARCHAR(MAX), -- not used
  @Folders NVARCHAR(MAX), -- not used
  @Attachments NVARCHAR(MAX), -- not used
  @CreationDate DATETIME2(7),
  @RevisionDate DATETIME2(7),
  @PwnedCheckDate DATETIME2(7),
  @Pwned BIT,
  @FolderId UNIQUEIDENTIFIER,
  @Favorite BIT,
  @Edit BIT, -- not used
  @OrganizationUseTotp BIT -- not used
AS
BEGIN
  SET NOCOUNT ON

  DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
  DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

  INSERT INTO [dbo].[Cipher]
  (
    [Id],
    [UserId],
    [OrganizationId],
    [Type],
    [Data],
    [Favorites],
    [Folders],
    [CreationDate],
    [RevisionDate],
    [PwnedCheckDate],
    [Pwned]
  )
  VALUES
  (
    @Id,
    CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
    @OrganizationId,
    @Type,
    @Data,
    CASE WHEN @Favorite = 1 THEN CONCAT('{', @UserIdKey, ':true}') ELSE NULL END,
    CASE WHEN @FolderId IS NOT NULL THEN CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}') ELSE NULL END,
    @CreationDate,
    @RevisionDate,
    @PwnedCheckDate,
    @Pwned
  )

  IF @OrganizationId IS NOT NULL
    BEGIN
      EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
  ELSE IF @UserId IS NOT NULL
    BEGIN
      EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END

GO
PRINT N'Updating [dbo].[CipherDetails_Update]...';

IF OBJECT_ID('[dbo].[CipherDetails_Update]') IS NOT NULL
  BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_Update]
  END
GO

GO
CREATE PROCEDURE [dbo].[CipherDetails_Update]
  @Id UNIQUEIDENTIFIER,
  @UserId UNIQUEIDENTIFIER,
  @OrganizationId UNIQUEIDENTIFIER,
  @Type TINYINT,
  @Data NVARCHAR(MAX),
  @Favorites NVARCHAR(MAX), -- not used
  @Folders NVARCHAR(MAX), -- not used
  @Attachments NVARCHAR(MAX), -- not used
  @CreationDate DATETIME2(7),
  @RevisionDate DATETIME2(7),
  @PwnedCheckDate DATETIME2(7),
  @Pwned BIT,
  @FolderId UNIQUEIDENTIFIER,
  @Favorite BIT,
  @Edit BIT, -- not used
  @OrganizationUseTotp BIT -- not used
AS
BEGIN
  SET NOCOUNT ON

  DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
  DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

  UPDATE
    [dbo].[Cipher]
  SET
    [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
    [OrganizationId] = @OrganizationId,
    [Type] = @Type,
    [Data] = @Data,
    [Folders] =
      CASE
        WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
          CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
        WHEN @FolderId IS NOT NULL THEN
          JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
        ELSE
          JSON_MODIFY([Folders], @UserIdPath, NULL)
        END,
    [Favorites] =
      CASE
        WHEN @Favorite = 1 AND [Favorites] IS NULL THEN
          CONCAT('{', @UserIdKey, ':true}')
        WHEN @Favorite = 1 THEN
          JSON_MODIFY([Favorites], @UserIdPath, CAST(1 AS BIT))
        ELSE
          JSON_MODIFY([Favorites], @UserIdPath, NULL)
        END,
    [CreationDate] = @CreationDate,
    [RevisionDate] = @RevisionDate,
    [PwnedCheckDate] = @PwnedCheckDate,
    [Pwned] = @Pwned
  WHERE
      [Id] = @Id

  IF @OrganizationId IS NOT NULL
    BEGIN
      EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
  ELSE IF @UserId IS NOT NULL
    BEGIN
      EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END

GO
PRINT N'Updating [dbo].[CipherDetails](@UserId)...';

IF OBJECT_ID('[dbo].[CipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[CipherDetails]
END
GO

CREATE FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
  RETURNS TABLE
  AS RETURN
SELECT
  C.[Id],
  C.[UserId],
  C.[OrganizationId],
  C.[Type],
  C.[Data],
  C.[Attachments],
  C.[CreationDate],
  C.[RevisionDate],
  C.[PwnedCheckDate],
  C.[Pwned],
  CASE
  WHEN
  @UserId IS NULL
         OR C.[Favorites] IS NULL
         OR JSON_VALUE(C.[Favorites], CONCAT('$."', @UserId, '"')) IS NULL
  THEN 0
ELSE 1
END [Favorite],
  CASE
  WHEN
  @UserId IS NULL
         OR C.[Folders] IS NULL
  THEN NULL
ELSE TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(C.[Folders], CONCAT('$."', @UserId, '"')))
END [FolderId]
  FROM
  [dbo].[Cipher] C

GO
IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
  BEGIN
    DROP FUNCTION [dbo].[UserCipherDetails]
  END
GO

GO
PRINT N'Updating [dbo].[UserCipherDetails](@UserId)...';

GO
CREATE FUNCTION [dbo].[UserCipherDetails](@UserId UNIQUEIDENTIFIER)
  RETURNS TABLE
    AS RETURN
    WITH [CTE] AS (
      SELECT
        [Id],
        [OrganizationId],
        [AccessAll]
      FROM
        [OrganizationUser]
      WHERE
          [UserId] = @UserId
        AND [Status] = 2 -- Confirmed
      )
      SELECT
        C.*,
        CASE
          WHEN
                OU.[AccessAll] = 1
              OR CU.[ReadOnly] = 0
              OR G.[AccessAll] = 1
              OR CG.[ReadOnly] = 0
            THEN 1
          ELSE 0
          END [Edit],
        CASE
          WHEN O.[UseTotp] = 1
            THEN 1
          ELSE 0
          END [OrganizationUseTotp]
      FROM
        [dbo].[CipherDetails](@UserId) C
          INNER JOIN
        [CTE] OU ON C.[UserId] IS NULL AND C.[OrganizationId] IN (SELECT [OrganizationId] FROM [CTE])
          INNER JOIN
        [dbo].[Organization] O ON O.[Id] = OU.OrganizationId AND O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
          LEFT JOIN
        [dbo].[CollectionCipher] CC ON OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
          LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
          LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
          LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
          LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
      WHERE
          OU.[AccessAll] = 1
         OR CU.[CollectionId] IS NOT NULL
         OR G.[AccessAll] = 1
         OR CG.[CollectionId] IS NOT NULL

      UNION ALL

      SELECT
        *,
        1 [Edit],
        0 [OrganizationUseTotp]
      FROM
        [dbo].[CipherDetails](@UserId)
      WHERE
        [UserId] = @UserId
