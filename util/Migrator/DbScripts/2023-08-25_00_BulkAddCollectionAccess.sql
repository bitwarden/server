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
    [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = C.[Id]
    LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = C.[Id]
WHERE
    OU.[OrganizationId] = @OrganizationId
  AND OU.[Status] = 2 -- 2 = Confirmed
  AND (
    CU.[CollectionId] IS NOT NULL
   OR CG.[CollectionId] IS NOT NULL
   OR OU.[AccessAll] = 1
   OR G.[AccessAll] = 1
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_CreateOrUpdateAccessForMany]
	@OrganizationId UNIQUEIDENTIFIER,
	@CollectionIds AS [dbo].[GuidIdArray] READONLY,
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY,
    @Users AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
	SET NOCOUNT ON

	 -- Groups
	;WITH [NewCollectionGroups] AS (
		SELECT
			cId.[Id] AS [CollectionId],
			gu.[Id] AS [GroupId],
			gu.[ReadOnly],
			gu.[HidePasswords]
		FROM
			@Groups AS gu
		CROSS JOIN
			@CollectionIds cId
		INNER JOIN
			[dbo].[Group] g ON gu.[Id] = g.[Id]
		WHERE
			g.[OrganizationId] = @OrganizationId
	)
    MERGE
    	[dbo].[CollectionGroup] as [Target]
	USING
		[NewCollectionGroups] AS [Source]
	ON
		[Target].[CollectionId] = [Source].[CollectionId]
		AND [Target].[GroupId] = [Source].[GroupId]
	WHEN MATCHED AND EXISTS(
		SELECT [Source].[ReadOnly], [Source].[HidePasswords]
		EXCEPT
		SELECT [Target].[ReadOnly], [Target].[HidePasswords]
	) THEN UPDATE SET
		[Target].[ReadOnly] = [Source].[ReadOnly],
		[Target].[HidePasswords] = [Source].[HidePasswords]
	WHEN NOT MATCHED BY TARGET
		THEN INSERT VALUES
		(
			[Source].[CollectionId],
			[Source].[GroupId],
			[Source].[ReadOnly],
			[Source].[HidePasswords]
		);

	-- Users
	;WITH [NewCollectionUsers] AS (
		SELECT
			cId.[Id] AS [CollectionId],
			cu.[Id] AS [OrganizationUserId],
			cu.[ReadOnly],
			cu.[HidePasswords]
		FROM
			@Users AS cu
		CROSS JOIN
			@CollectionIds cId
		INNER JOIN
			[dbo].[OrganizationUser] u ON cu.[Id] = u.[Id]
		WHERE
			u.[OrganizationId] = @OrganizationId
	)
    MERGE
    	[dbo].[CollectionUser] as [Target]
	USING
		[NewCollectionUsers] AS [Source]
	ON
		[Target].[CollectionId] = [Source].[CollectionId]
		AND [Target].[OrganizationUserId] = [Source].[OrganizationUserId]
	WHEN MATCHED AND EXISTS(
		SELECT [Source].[ReadOnly], [Source].[HidePasswords]
		EXCEPT
		SELECT [Target].[ReadOnly], [Target].[HidePasswords]
	) THEN UPDATE SET
		[Target].[ReadOnly] = [Source].[ReadOnly],
		[Target].[HidePasswords] = [Source].[HidePasswords]
	WHEN NOT MATCHED BY TARGET
		THEN INSERT VALUES
		(
			[Source].[CollectionId],
			[Source].[OrganizationUserId],
			[Source].[ReadOnly],
			[Source].[HidePasswords]
		);

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @CollectionIds, @OrganizationId
END
GO
