-- Collection_ReadWithGroupsByOrganizationId
IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[Collection_ReadByOrganizationId] @OrganizationId

    EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
    
END
GO

-- Collection_ReadWithGroupsByUserIdOrganizationId
IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsByUserIdOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsByUserIdOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsByUserIdOrganizationId]
	@UserId UNIQUEIDENTIFIER,
	@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON
	
	DECLARE @TempUserCollections TABLE(Id UNIQUEIDENTIFIER, OrganizationId UNIQUEIDENTIFIER, Name VARCHAR(MAX), CreationDate DATETIME2(7), RevisionDate DATETIME2(7), ExternalId NVARCHAR(300), ReadOnly BIT, HidePasswords BIT)

	INSERT INTO @TempUserCollections EXEC [dbo].[Collection_ReadByUserId] @UserId
	 
	SELECT
		*
	FROM
	 	 @TempUserCollections C
	WHERE
		C.[OrganizationId] = @OrganizationId
	 	 
	SELECT
		CG.*
	FROM
	 	[dbo].[CollectionGroup] CG
	INNER JOIN
	    @TempUserCollections C ON C.[Id] = CG.[CollectionId]
	WHERE
	    C.[OrganizationId] = @OrganizationId

END
GO

-- Collection_ReadByIds
IF OBJECT_ID('[dbo].[Collection_ReadByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByIds]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    SELECT
        *
    FROM
        [dbo].[Collection]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
END
GO

-- Collection_DeleteByIds
IF OBJECT_ID('[dbo].[Collection_DeleteByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_DeleteByIds]
END
GO

CREATE PROCEDURE [dbo].[Collection_DeleteByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
	
    -- Delete Collection Groups
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION CollectionGroup_DeleteMany
        	DELETE TOP(@BatchSize) 
        	FROM
        		[dbo].[CollectionGroup]
        	WHERE
                [CollectionId] IN (SELECT [Id] FROM @Ids)


            SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION CollectionGroup_DeleteMany
    END
    
    -- Reset batch size
    SET @BatchSize = 100

    -- Delete Collections
    WHILE @BatchSize > 0
    BEGIN
	    BEGIN TRANSACTION Collection_DeleteMany
            DELETE TOP(@BatchSize)
            FROM
                [dbo].[Collection]
            WHERE
                [Id] IN (SELECT [Id] FROM @Ids)
            
            SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION CollectionGroup_DeleteMany
	END
END
GO