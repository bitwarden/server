-- Remove AccessAll logic from bump account revision date sprocs

-- User_BumpAccountRevisionDateByCipherId
CREATE OR ALTER PROCEDURE [dbo].[User_BumpAccountRevisionDateByCipherId]
    @CipherId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON CC.[CipherId] = @CipherId
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = CC.[CollectionId]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = CC.[CollectionId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO

-- User_BumpAccountRevisionDateByCollectionId
CREATE OR ALTER PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = @CollectionId
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO

-- User_BumpAccountRevisionDateByCollectionIds
CREATE OR ALTER PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionIds]
	@CollectionIds AS [dbo].[GuidIdArray] READONLY,
	@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON

UPDATE
    U
SET
    U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
    	[dbo].[Collection] C ON C.[Id] IN (SELECT [Id] FROM @CollectionIds)
    INNER JOIN
    [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = C.[Id]
    LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
    [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = C.[Id]
WHERE
    OU.[OrganizationId] = @OrganizationId
  AND OU.[Status] = 2 -- 2 = Confirmed
  AND (
    CU.[CollectionId] IS NOT NULL
   OR CG.[CollectionId] IS NOT NULL
  )
END
GO
