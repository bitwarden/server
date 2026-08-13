CREATE PROCEDURE [dbo].[Collection_CreateOrUpdateAccessForMany]
	@OrganizationId UNIQUEIDENTIFIER,
	@CollectionIds AS [dbo].[GuidIdArray] READONLY,
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY,
    @RevisionDate DATETIME2(7) = NULL
AS
BEGIN
	SET NOCOUNT ON

	 -- Groups
	;WITH [NewCollectionGroups] AS (
		SELECT
			cId.[Id] AS [CollectionId],
			cg.[Id] AS [GroupId],
			cg.[ReadOnly],
			cg.[HidePasswords],
			cg.[Manage]
		FROM
			@Groups AS cg
		CROSS JOIN -- Create a CollectionGroup record for every CollectionId
			@CollectionIds cId
		INNER JOIN
			[dbo].[Group] g ON cg.[Id] = g.[Id]
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
    -- Update the target if any values are different from the source
	WHEN MATCHED AND EXISTS(
		SELECT [Source].[ReadOnly], [Source].[HidePasswords], [Source].[Manage]
		EXCEPT
		SELECT [Target].[ReadOnly], [Target].[HidePasswords], [Target].[Manage]
	) THEN UPDATE SET
		[Target].[ReadOnly] = [Source].[ReadOnly],
		[Target].[HidePasswords] = [Source].[HidePasswords],
		[Target].[Manage] = [Source].[Manage]
	WHEN NOT MATCHED BY TARGET
		THEN INSERT
	    (
	        [CollectionId],
	        [GroupId],
	        [ReadOnly],
	        [HidePasswords],
	        [Manage]
        )
	    VALUES
		(
			[Source].[CollectionId],
			[Source].[GroupId],
			[Source].[ReadOnly],
			[Source].[HidePasswords],
			[Source].[Manage]
		);

	-- Users
	;WITH [NewCollectionUsers] AS (
		SELECT
			cId.[Id] AS [CollectionId],
			cu.[Id] AS [OrganizationUserId],
			cu.[ReadOnly],
			cu.[HidePasswords],
			cu.[Manage]
		FROM
			@Users AS cu
		CROSS JOIN -- Create a CollectionUser record for every CollectionId
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
    -- Update the target if any values are different from the source
	WHEN MATCHED AND EXISTS(
		SELECT [Source].[ReadOnly], [Source].[HidePasswords], [Source].[Manage]
		EXCEPT
		SELECT [Target].[ReadOnly], [Target].[HidePasswords], [Target].[Manage]
	) THEN UPDATE SET
		[Target].[ReadOnly] = [Source].[ReadOnly],
		[Target].[HidePasswords] = [Source].[HidePasswords],
		[Target].[Manage] = [Source].[Manage]
	WHEN NOT MATCHED BY TARGET
	    THEN INSERT
	    (
	        [CollectionId],
	        [OrganizationUserId],
	        [ReadOnly],
	        [HidePasswords],
	        [Manage]
        )
	    VALUES
		(
			[Source].[CollectionId],
			[Source].[OrganizationUserId],
			[Source].[ReadOnly],
			[Source].[HidePasswords],
			[Source].[Manage]
		);

    IF @RevisionDate IS NOT NULL
    BEGIN
		-- Bump the revision date on all affected collections
        UPDATE
            C
        SET
            C.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Collection] C
        INNER JOIN
            @CollectionIds CI ON C.[Id] = CI.[Id]

        -- Bump the revision date on all affected groups
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        INNER JOIN
            @Groups GR ON G.[Id] = GR.[Id]
        WHERE
            G.[OrganizationId] = @OrganizationId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @CollectionIds, @OrganizationId
END
